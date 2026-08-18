using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Application.Reports.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// خروجی‌گیری گزارش‌ها (CSV/HTML) — فاز محصول‌سازی (P5، کارهای #۲ و #۳).
/// مالی + فروش/انبار/شعب/مشتری/تأمین‌کننده روی موتور مشترک ReportExporter.
/// query: format=csv|html (پیش‌فرض csv).
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/reports/export")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportsController(IMediator mediator) => _mediator = mediator;

    // ── مالی ──────────────────────────────────────────────────────────────────
    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance(string from, string to, string format = "csv", CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetTrialBalanceQuery(from, to), ct);
        var t = ReportTable.From("تراز آزمایشی", new[] { "کد", "نام", "بدهکار", "بستانکار", "مانده" },
            rows, r => new object?[] { r.Code, r.Name, r.Debit, r.Credit, r.Balance });
        return Export(t, format, "trial-balance");
    }

    [HttpGet("general-ledger")]
    public async Task<IActionResult> GeneralLedger(string from, string to, int? accountId, string format = "csv", CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetGeneralLedgerQuery(from, to, accountId), ct);
        var t = ReportTable.From("دفتر کل/معین", new[] { "تاریخ", "شماره سند", "کد", "نام", "شرح", "بدهکار", "بستانکار", "مانده" },
            rows, r => new object?[] { r.Date, r.VoucherNumber, r.Code, r.Name, r.Description, r.Debit, r.Credit, r.Balance });
        return Export(t, format, "general-ledger");
    }

    [HttpGet("profit-loss")]
    public async Task<IActionResult> ProfitLoss(string from, string to, string format = "csv", CancellationToken ct = default)
    {
        var pl = await _mediator.Send(new GetProfitLossQuery(from, to), ct);
        var rows = new List<string[]>();
        foreach (var r in pl.Revenues) rows.Add(new[] { "درآمد", r.Code, r.Name, Num(r.Amount) });
        foreach (var e in pl.Expenses) rows.Add(new[] { "هزینه", e.Code, e.Name, Num(e.Amount) });
        rows.Add(new[] { "—", "", "جمع درآمد", Num(pl.Revenue) });
        rows.Add(new[] { "—", "", "جمع هزینه", Num(pl.Expense) });
        rows.Add(new[] { "—", "", "سود/زیان خالص", Num(pl.NetProfit) });
        var t = new ReportTable("سود و زیان", new[] { "نوع", "کد", "نام", "مبلغ" }, rows);
        return Export(t, format, "profit-loss");
    }

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> BalanceSheet(string from, string to, string format = "csv", CancellationToken ct = default)
    {
        var bs = await _mediator.Send(new GetBalanceSheetQuery(from, to), ct);
        var rows = new List<string[]>();
        foreach (var r in bs.Assets) rows.Add(new[] { "دارایی", r.Code, r.Name, Num(r.Amount) });
        rows.Add(new[] { "—", "", "جمع دارایی‌ها", Num(bs.TotalAssets) });
        foreach (var r in bs.Liabilities) rows.Add(new[] { "بدهی", r.Code, r.Name, Num(r.Amount) });
        rows.Add(new[] { "—", "", "جمع بدهی‌ها", Num(bs.TotalLiabilities) });
        foreach (var r in bs.Equity) rows.Add(new[] { "حقوق صاحبان سهام", r.Code, r.Name, Num(r.Amount) });
        rows.Add(new[] { "—", "", "جمع حقوق صاحبان سهام", Num(bs.TotalEquity) });
        rows.Add(new[] { "—", "", "وضعیتِ تراز", bs.IsBalanced ? "متوازن" : "نامتوازن" });
        var t = new ReportTable("ترازنامه", new[] { "نوع", "کد", "نام", "مبلغ" }, rows);
        return Export(t, format, "balance-sheet");
    }

    [HttpGet("cash-flow")]
    public async Task<IActionResult> CashFlow(string from, string to, string format = "csv", CancellationToken ct = default)
    {
        var cf = await _mediator.Send(new GetCashFlowQuery(from, to), ct);
        var rows = new List<string[]>
        {
            new[] { "عملیاتی", Num(cf.Operating) },
            new[] { "سرمایه‌گذاری", Num(cf.Investing) },
            new[] { "تأمین مالی", Num(cf.Financing) },
            new[] { "خالص تغییر نقد", Num(cf.NetChange) },
            new[] { "موجودی نقد اول دوره", Num(cf.OpeningCash) },
            new[] { "موجودی نقد پایان دوره", Num(cf.ClosingCash) },
        };
        var t = new ReportTable("جریان وجوه نقد", new[] { "بخش", "مبلغ" }, rows);
        return Export(t, format, "cash-flow");
    }

    // ── فروش / BI ───────────────────────────────────────────────────────────────
    [HttpGet("top-customers")]
    public async Task<IActionResult> TopCustomers(string from, string to, int take = 20, string format = "csv", CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetTopCustomersQuery(from, to, take), ct);
        var t = ReportTable.From("مشتریان برتر", new[] { "کد", "نام", "مبلغ خرید", "تعداد فاکتور" },
            rows, r => new object?[] { r.CustomerId, r.Name, r.Total, r.InvoiceCount });
        return Export(t, format, "top-customers");
    }

    [HttpGet("top-suppliers")]
    public async Task<IActionResult> TopSuppliers(string from, string to, int take = 20, string format = "csv", CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetTopSuppliersQuery(from, to, take), ct);
        var t = ReportTable.From("تأمین‌کنندگان برتر", new[] { "کد", "نام", "مبلغ خرید", "تعداد فاکتور" },
            rows, r => new object?[] { r.SupplierId, r.Name, r.Total, r.InvoiceCount });
        return Export(t, format, "top-suppliers");
    }

    [HttpGet("branch-performance")]
    public async Task<IActionResult> BranchPerformance(string from, string to, string format = "csv", CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetBranchPerformanceQuery(from, to), ct);
        var t = ReportTable.From("عملکرد شعب", new[] { "کد شعبه", "نام", "فروش", "تعداد فاکتور" },
            rows, r => new object?[] { r.BranchId, r.Name, r.Total, r.InvoiceCount });
        return Export(t, format, "branch-performance");
    }

    [HttpGet("sales-trend")]
    public async Task<IActionResult> SalesTrend(string from, string to, string format = "csv", CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetSalesTrendQuery(from, to), ct);
        var t = ReportTable.From("روند فروش ماهانه", new[] { "ماه", "فروش", "تعداد" },
            rows, r => new object?[] { r.Period, r.Total, r.Count });
        return Export(t, format, "sales-trend");
    }

    [HttpGet("inventory-trend")]
    public async Task<IActionResult> InventoryTrend(string from, string to, int? productId, string format = "csv", CancellationToken ct = default)
    {
        var rows = await _mediator.Send(new GetInventoryTrendQuery(from, to, productId), ct);
        var t = ReportTable.From("روند موجودی", new[] { "ماه", "ورود", "خروج", "خالص" },
            rows, r => new object?[] { r.Period, r.InQty, r.OutQty, r.Net });
        return Export(t, format, "inventory-trend");
    }

    // ── helper ──────────────────────────────────────────────────────────────────
    private static string Num(decimal d) => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private IActionResult Export(ReportTable t, string format, string fileBase)
    {
        if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
            return File(Encoding.UTF8.GetBytes(ReportExporter.ToHtml(t)), "text/html; charset=utf-8", fileBase + ".html");
        // BOM تا اکسل فارسی را درست بخواند
        var csv = "﻿" + ReportExporter.ToCsv(t);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileBase + ".csv");
    }
}
