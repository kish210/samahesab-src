using MediatR;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>
/// U-REPORTS-CENTER — «مرکزِ گزارشات» پیش‌تر برایِ هیچ‌کدام از ۲۲ گزارش کوئریِ واقعی نداشت
/// (فقط پیامِ «هنوز پیاده‌سازی نشده»). این کوئری، برایِ گزارش‌هایِ متعلق به هسته (نه ماژول‌هایِ
/// اختیاریِ HR/Attendance که جدا و در WPF مستقیم صدا زده می‌شوند)، کدِ گزارش را به کوئریِ
/// موجودِ متناظر وصل می‌کند و نتیجه را به شکلِ یکدستِ (Code,Name,Debit,Credit,Balance) درمی‌آورد.
/// «دفترِ معین» و «کاردکسِ کالا» عمداً این‌جا نیستند — نیازِ انتخابِ حساب/کالایِ مشخص دارند که این
/// صفحهٔ عمومی (فقط بازهٔ تاریخ) ندارد؛ کاربر باید از صفحهٔ اختصاصیِ خودشان استفاده کند.
/// </summary>
public record ReportRunRow(string Code, string Name, decimal Debit, decimal Credit, decimal Balance);

public record RunReportQuery(string ReportCode, string FromDate, string ToDate) : IRequest<ReportRunResult>;

/// <summary>Rows=null یعنی این کد این‌جا پشتیبانی نمی‌شود (RedirectMessage توضیح می‌دهد چرا).</summary>
public record ReportRunResult(List<ReportRunRow>? Rows, string? RedirectMessage);

public class RunReportQueryHandler : IRequestHandler<RunReportQuery, ReportRunResult>
{
    private readonly IMediator _mediator;
    public RunReportQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<ReportRunResult> Handle(RunReportQuery req, CancellationToken ct)
    {
        List<ReportRunRow> Rows(IEnumerable<ReportRunRow> rows) => rows.ToList();
        // U-REPORTS-CENTER: ستون‌هایِ «بدهکار»/«بستانکار» نباید عددِ منفی نشان دهند — اگر ماندهٔ
        // یک ردیف برخلافِ طبیعتِ معمولش شد (مثلاً حسابِ درآمدی که این دوره خالص بدهکار است،
        // بابتِ برگشت/کنسینمنتِ بیشتر از فروش)، مقدار به ستونِ مقابل (با قدرِمطلق) منتقل می‌شود.
        (decimal natural, decimal contra) Signed(decimal amount) => amount >= 0 ? (amount, 0) : (0, -amount);

        switch (req.ReportCode)
        {
            case "TrialBalance":
            {
                var rows = await _mediator.Send(new GetTrialBalanceQuery(req.FromDate, req.ToDate), ct);
                return new(Rows(rows.Select(r => new ReportRunRow(r.Code, r.Name, r.Debit, r.Credit, r.Balance))), null);
            }
            case "GeneralLedger":
            {
                var rows = await _mediator.Send(new GetGeneralLedgerQuery(req.FromDate, req.ToDate, null), ct);
                return new(Rows(rows.Select(r => new ReportRunRow(
                    r.VoucherNumber, $"{r.Date} — {r.Code} {r.Name}" + (string.IsNullOrWhiteSpace(r.Description) ? "" : $" ({r.Description})"),
                    r.Debit, r.Credit, r.Balance))), null);
            }
            case "DetailLedger":
                return new(null, "برایِ «دفترِ معین» نیازِ انتخابِ یک حسابِ مشخص است — از منویِ «حسابداری ▸ دفترِ معین» استفاده کنید.");

            case "BalanceSheet":
            {
                var d = await _mediator.Send(new GetBalanceSheetQuery(req.FromDate, req.ToDate), ct);
                var rows = d.Assets.Select(l => { var (n, c) = Signed(l.Amount); return new ReportRunRow(l.Code, "دارایی — " + l.Name, n, c, 0); })
                    .Concat(d.Liabilities.Select(l => { var (n, c) = Signed(l.Amount); return new ReportRunRow(l.Code, "بدهی — " + l.Name, c, n, 0); }))
                    .Concat(d.Equity.Select(l => { var (n, c) = Signed(l.Amount); return new ReportRunRow(l.Code, "سرمایه — " + l.Name, c, n, 0); }))
                    .Append(new ReportRunRow("—", "جمعِ دارایی‌ها", d.TotalAssets, 0, 0))
                    .Append(new ReportRunRow("—", "جمعِ بدهی‌ها + سرمایه", 0, d.TotalLiabilities + d.TotalEquity, 0))
                    .Append(new ReportRunRow("—", d.IsBalanced ? "ترازنامه متوازن است" : "⚠ ترازنامه متوازن نیست", 0, 0, d.NetProfit));
                return new(Rows(rows), null);
            }
            case "ProfitLoss":
            {
                var d = await _mediator.Send(new GetProfitLossQuery(req.FromDate, req.ToDate), ct);
                var rows = d.Revenues.Select(l => { var (n, c) = Signed(l.Amount); return new ReportRunRow(l.Code, l.Name, c, n, 0); })
                    .Concat(d.Expenses.Select(l => { var (n, c) = Signed(l.Amount); return new ReportRunRow(l.Code, l.Name, n, c, 0); }))
                    .Append(new ReportRunRow("—", "جمعِ درآمد", 0, d.Revenue, 0))
                    .Append(new ReportRunRow("—", "جمعِ هزینه", d.Expense, 0, 0))
                    .Append(new ReportRunRow("—", "سودِ خالص", 0, 0, d.NetProfit));
                return new(Rows(rows), null);
            }
            case "CashFlow":
            {
                var d = await _mediator.Send(new GetCashFlowQuery(req.FromDate, req.ToDate), ct);
                var rows = new[]
                {
                    new ReportRunRow("OP", "جریانِ عملیاتی", d.Operating >= 0 ? d.Operating : 0, d.Operating < 0 ? -d.Operating : 0, 0),
                    new ReportRunRow("INV", "جریانِ سرمایه‌گذاری", d.Investing >= 0 ? d.Investing : 0, d.Investing < 0 ? -d.Investing : 0, 0),
                    new ReportRunRow("FIN", "جریانِ تأمینِ مالی", d.Financing >= 0 ? d.Financing : 0, d.Financing < 0 ? -d.Financing : 0, 0),
                    new ReportRunRow("—", "خالصِ تغییرِ وجهِ نقد", 0, 0, d.NetChange),
                    new ReportRunRow("—", "موجودیِ نقدِ ابتدایِ دوره", 0, 0, d.OpeningCash),
                    new ReportRunRow("—", "موجودیِ نقدِ انتهایِ دوره", 0, 0, d.ClosingCash),
                };
                return new(Rows(rows), null);
            }
            case "SalesSummary":
            {
                var d = await _mediator.Send(new SamaHesab.Application.BI.Queries.GetProfitAnalysisQuery(req.FromDate, req.ToDate), ct);
                var rows = new[]
                {
                    new ReportRunRow("—", "جمعِ فروش", d.Sales, 0, 0),
                    new ReportRunRow("—", "سودِ ناخالص", 0, d.Profit, 0),
                    new ReportRunRow("—", "درصدِ حاشیهٔ سود", 0, 0, d.MarginPercent),
                };
                return new(Rows(rows), null);
            }
            case "SalesByCustomer":
            {
                var rows = await _mediator.Send(new SamaHesab.Application.BI.Queries.GetTopCustomersQuery(req.FromDate, req.ToDate, 100), ct);
                return new(Rows(rows.Select(r => new ReportRunRow(r.CustomerId.ToString(), r.Name, r.Total, 0, r.InvoiceCount))), null);
            }
            case "SalesByProduct":
            {
                var d = await _mediator.Send(new SamaHesab.Application.BI.Queries.GetProfitAnalysisQuery(req.FromDate, req.ToDate, 100), ct);
                return new(Rows(d.TopProducts.Select(r => new ReportRunRow(r.ProductId.ToString(), r.Name, r.Total, r.Profit, r.LineCount))), null);
            }
            case "CustomerBalance":
            {
                var rows = await _mediator.Send(new SamaHesab.Application.Treasury.Queries.GetReceivablesQuery(500), ct);
                return new(Rows(rows.Select(r => new ReportRunRow(r.CustomerId.ToString(), r.Name, r.Balance, r.CreditLimit, r.CreditLimit - r.Balance))), null);
            }
            case "PurchaseSummary":
            {
                var rows = await _mediator.Send(new SamaHesab.Application.BI.Queries.GetPurchaseTrendQuery(req.FromDate, req.ToDate), ct);
                var totalAmount = rows.Sum(r => r.Total);
                var totalCount = rows.Sum(r => r.Count);
                var result = new[]
                {
                    new ReportRunRow("—", "جمعِ خرید", totalAmount, 0, 0),
                    new ReportRunRow("—", "تعدادِ فاکتور", 0, 0, totalCount),
                };
                return new(Rows(result), null);
            }
            case "SupplierBalance":
            {
                var rows = await _mediator.Send(new SamaHesab.Application.Treasury.Queries.GetPayablesQuery(500), ct);
                return new(Rows(rows.Select(r => new ReportRunRow(r.SupplierId.ToString(), r.Name, 0, r.Balance, r.Balance))), null);
            }
            case "StockStatus":
            {
                var d = await _mediator.Send(new SamaHesab.Application.Inventory.Queries.GetInventoryValuationQuery(), ct);
                return new(Rows(d.Rows.Select(r => new ReportRunRow(r.Code, r.Name, r.Quantity, 0, r.Value))), null);
            }
            case "StockMovement":
                return new(null, "برایِ «گردشِ کالا» نیازِ انتخابِ یک کالایِ مشخص است — از منویِ «انبار ▸ کاردکسِ کالا» استفاده کنید.");

            case "LowStock":
            {
                var d = await _mediator.Send(new SamaHesab.Application.Inventory.Queries.GetReorderReportQuery(), ct);
                return new(Rows(d.Rows.Select(r => new ReportRunRow(r.Code, r.Name, r.OnHand, r.MinStock, r.Shortage))), null);
            }
            case "StockValuation":
            {
                var d = await _mediator.Send(new SamaHesab.Application.Inventory.Queries.GetInventoryValuationQuery(), ct);
                var rows = d.Rows.Select(r => new ReportRunRow(r.Code, r.Name, r.Quantity, r.AverageCost, r.Value))
                    .Append(new ReportRunRow("—", "جمعِ ارزشِ موجودی", 0, 0, d.TotalValue));
                return new(Rows(rows), null);
            }
            case "ChequesInProcess":
            {
                var rows = await _mediator.Send(new SamaHesab.Application.Accounting.Queries.GetChequeBoardQuery(req.ToDate), ct);
                return new(Rows(rows.Select(r => new ReportRunRow(r.ChequeNumber, $"{r.BankName} ({r.Type}) — سررسید {r.DueDate}",
                    r.Type == "دریافتی" ? r.Amount : 0, r.Type == "پرداختی" ? r.Amount : 0, 0))), null);
            }
            case "ChequesDue":
            {
                var rows = await _mediator.Send(new SamaHesab.Application.Accounting.Queries.GetChequeBoardQuery(req.ToDate), ct);
                var due = rows.Where(r => r.DueState is "Overdue" or "DueToday");
                return new(Rows(due.Select(r => new ReportRunRow(r.ChequeNumber, $"{r.BankName} ({r.Type}) — {r.DueState}",
                    r.Type == "دریافتی" ? r.Amount : 0, r.Type == "پرداختی" ? r.Amount : 0, 0))), null);
            }
            case "ChequesReturned":
            {
                var rows = await _mediator.Send(new SamaHesab.Application.Accounting.Queries.GetChequesQuery(), ct);
                var returned = rows.Where(r => r.Status == ChequeStatus.Returned);
                return new(Rows(returned.Select(r => new ReportRunRow(r.ChequeNumber, $"{r.BankName} — {r.Description}",
                    r.ChequeType == ChequeType.Received ? r.Amount : 0, r.ChequeType == ChequeType.Paid ? r.Amount : 0, 0))), null);
            }
            case "EmployeeList":
            {
                var rows = await _mediator.Send(new SamaHesab.Application.HRM.GetEmployeesQuery(), ct);
                return new(Rows(rows.Select(r => new ReportRunRow(r.Code, r.FullName, r.BaseSalary, 0, 0))), null);
            }

            // SalaryReport/AttendanceSummary عمداً این‌جا نیستند — ماژول‌هایِ اختیاریِ HR/Attendance
            // (طبقِ CLAUDE.md، هسته هرگز به پیاده‌سازیِ ماژول وابسته نیست)؛ WPF مستقیم صدا می‌زند.
            default:
                return new(null, $"گزارشِ «{req.ReportCode}» شناخته‌شده نیست.");
        }
    }
}
