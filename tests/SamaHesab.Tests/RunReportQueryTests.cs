using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.Domain.Enums;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-REPORTS-CENTER — پیش‌تر «مرکزِ گزارشات» برایِ هیچ‌کدام از ۲۲ گزارش کوئریِ واقعی
/// نداشت. این تست‌ها RunReportQueryHandler را با یک IMediatorِ جعلی (پاسخِ ازپیش‌مشخص به‌ازایِ
/// نوعِ درخواست) بررسی می‌کنند — منطقِ خودِ کوئری‌هایِ زیرین جای دیگر تست شده‌اند.</summary>
public class RunReportQueryTests
{
    private sealed class FakeMediator : IMediator
    {
        private readonly Dictionary<System.Type, object> _responses = new();
        public void When<TRequest>(object response) => _responses[typeof(TRequest)] = response;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (_responses.TryGetValue(request.GetType(), out var r)) return Task.FromResult((TResponse)r);
            throw new System.InvalidOperationException($"No canned response for {request.GetType().Name}");
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => null!;
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => null!;
        public Task Publish(object n, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    [Fact]
    public async Task TrialBalance_Passes_Through_Rows_Unchanged()
    {
        var mediator = new FakeMediator();
        mediator.When<GetTrialBalanceQuery>(new List<TrialBalanceRow> { new("1-01-001", "صندوق", 1000, 200, 800) });
        var handler = new RunReportQueryHandler(mediator);

        var result = await handler.Handle(new RunReportQuery("TrialBalance", "1404/01/01", "1404/01/31"), default);

        Assert.NotNull(result.Rows);
        var row = Assert.Single(result.Rows!);
        Assert.Equal("1-01-001", row.Code);
        Assert.Equal(800, row.Balance);
    }

    [Fact]
    public async Task BalanceSheet_Flattens_Assets_Liabilities_Equity_Plus_Totals()
    {
        var mediator = new FakeMediator();
        mediator.When<GetBalanceSheetQuery>(new BalanceSheetDto(
            Assets: new() { new("1-01", "نقد", 5000) },
            Liabilities: new() { new("3-01", "پرداختنی", 2000) },
            Equity: new() { new("5-01", "سرمایه", 3000) },
            TotalAssets: 5000, TotalLiabilities: 2000, TotalEquity: 3000, NetProfit: 0, IsBalanced: true));
        var handler = new RunReportQueryHandler(mediator);

        var result = await handler.Handle(new RunReportQuery("BalanceSheet", "1404/01/01", "1404/01/31"), default);

        Assert.NotNull(result.Rows);
        // ۳ ردیفِ خط + ۳ ردیفِ جمع = ۶
        Assert.Equal(6, result.Rows!.Count);
        Assert.Contains(result.Rows, r => r.Name.Contains("جمعِ دارایی‌ها") && r.Debit == 5000);
    }

    /// <summary>باگِ واقعیِ کشف‌شده در تستِ زندهٔ رویِ DBِ واقعی: یک حسابِ درآمدی که این دوره خالص
    /// بدهکار بود (مثلاً برگشت/کنسینمنتِ بیشتر از فروش)، مقدارِ منفی در ستونِ «بستانکار» نشان
    /// می‌داد. حالا باید به ستونِ «بدهکار» با قدرِمطلق منتقل شود.</summary>
    [Fact]
    public async Task ProfitLoss_Puts_Contra_Balance_Revenue_In_Debit_Column_Not_Negative_Credit()
    {
        var mediator = new FakeMediator();
        mediator.When<GetProfitLossQuery>(new ProfitLossDto(
            Revenue: -1000, Expense: 0, NetProfit: -1000,
            Revenues: new() { new("6-01-001", "درآمدِ فروش", -1000) },
            Expenses: new()));
        var handler = new RunReportQueryHandler(mediator);

        var result = await handler.Handle(new RunReportQuery("ProfitLoss", "1404/01/01", "1404/01/31"), default);

        var line = Assert.Single(result.Rows!, r => r.Code == "6-01-001");
        Assert.Equal(1000, line.Debit);
        Assert.Equal(0, line.Credit);
    }

    [Fact]
    public async Task ChequesReturned_Filters_Only_Returned_Status()
    {
        var mediator = new FakeMediator();
        mediator.When<GetChequesQuery>(new List<ChequeRowDto>
        {
            new(1, ChequeType.Received, "CH-1", "بانکِ ملت", 1_000_000, "1404/02/01", ChequeStatus.Returned, "علی", "برگشتی"),
            new(2, ChequeType.Paid, "CH-2", "بانکِ ملی", 2_000_000, "1404/02/05", ChequeStatus.Cleared, "رضا", "وصول‌شده"),
        });
        var handler = new RunReportQueryHandler(mediator);

        var result = await handler.Handle(new RunReportQuery("ChequesReturned", "1404/01/01", "1404/02/29"), default);

        Assert.NotNull(result.Rows);
        var row = Assert.Single(result.Rows!);
        Assert.Equal("CH-1", row.Code);
        Assert.Equal(1_000_000, row.Debit);   // دریافتی
    }

    [Fact]
    public async Task DetailLedger_Returns_Redirect_Message_Not_Rows()
    {
        var handler = new RunReportQueryHandler(new FakeMediator());
        var result = await handler.Handle(new RunReportQuery("DetailLedger", "1404/01/01", "1404/01/31"), default);

        Assert.Null(result.Rows);
        Assert.Contains("دفترِ معین", result.RedirectMessage);
    }

    [Fact]
    public async Task StockMovement_Returns_Redirect_Message_Not_Rows()
    {
        var handler = new RunReportQueryHandler(new FakeMediator());
        var result = await handler.Handle(new RunReportQuery("StockMovement", "1404/01/01", "1404/01/31"), default);

        Assert.Null(result.Rows);
        Assert.Contains("کاردکسِ کالا", result.RedirectMessage);
    }

    [Fact]
    public async Task Unknown_Report_Code_Returns_Redirect_Message()
    {
        var handler = new RunReportQueryHandler(new FakeMediator());
        var result = await handler.Handle(new RunReportQuery("NotARealReport", "1404/01/01", "1404/01/31"), default);

        Assert.Null(result.Rows);
        Assert.NotNull(result.RedirectMessage);
    }
}
