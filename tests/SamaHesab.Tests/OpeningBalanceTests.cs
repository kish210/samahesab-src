using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Common.Models;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ P-G6 — سندِ افتتاحیه: توازن اجباری و واگذاری به CreateVoucherCommand (نوعِ OPEN).</summary>
public class OpeningBalanceTests
{
    // مدیاتورِ ساختگی که CreateVoucherCommandِ ارسالی را ضبط می‌کند.
    private sealed class CapturingMediator : IMediator
    {
        public CreateVoucherCommand? Captured;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            Captured = request as CreateVoucherCommand;
            object ok = Result<int>.Success(777);
            return Task.FromResult((TResponse)ok);
        }
        // اعضای استفاده‌نشده:
        public Task<object?> Send(object request, CancellationToken ct = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => null!;
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => null!;
        public Task Publish(object n, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    [Fact]
    public async Task Rejects_Unbalanced_Opening()
    {
        var sut = new PostOpeningBalanceCommandHandler(new CapturingMediator());
        var res = await sut.Handle(new PostOpeningBalanceCommand(1, 1, "1405/01/01",
            new List<OpeningBalanceLine> { new(101, 5000, 0), new(201, 0, 4000) }), default);

        Assert.False(res.Succeeded);
    }

    [Fact]
    public async Task Posts_Balanced_Opening_As_OPEN_Type()
    {
        var med = new CapturingMediator();
        var sut = new PostOpeningBalanceCommandHandler(med);
        var res = await sut.Handle(new PostOpeningBalanceCommand(1, 7, "1405/01/01",
            new List<OpeningBalanceLine> { new(101, 5000, 0, "موجودیِ صندوق"), new(301, 0, 5000, "سرمایه") }), default);

        Assert.True(res.Succeeded);
        Assert.Equal(777, res.Value);
        Assert.NotNull(med.Captured);
        Assert.Equal(PostOpeningBalanceCommandHandler.OpeningVoucherTypeId, med.Captured!.VoucherTypeId);
        Assert.Equal(2, med.Captured.Items.Count);
        Assert.Equal(5000, med.Captured.Items[0].Debit);
    }
}
