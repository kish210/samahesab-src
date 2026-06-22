using System.Globalization;
using SamaHesab.Application.Reports.Export;

namespace SamaHesab.Application.Reports;

/// <summary>یک ردیفِ اظهارِ حساب (مستقل از منبع — مشتری یا تأمین‌کننده).</summary>
public record PartyStatementLine(string Date, string DocType, string DocNumber, string Description,
    decimal Debit, decimal Credit, decimal Balance);

/// <summary>
/// CR-C1 (#۶/۷) — سازندهٔ گزارشِ خروجی‌دارِ «اظهارِ حسابِ» مشتری/تأمین‌کننده.
/// خالص/تست‌پذیر؛ خروجی ReportTable که با ReportExporter به CSV/HTMLِ راست‌چین (PDF/Excel) تبدیل می‌شود.
/// دادهٔ ردیف‌ها از GetCustomerStatementQuery (و معادلِ تأمین‌کننده) می‌آید.
/// </summary>
public static class PartyStatementReportBuilder
{
    public static ReportTable Build(string partyName, IEnumerable<PartyStatementLine> rows,
        decimal totalDebit, decimal totalCredit, decimal closingBalance, bool isSupplier = false)
    {
        var list = rows?.ToList() ?? new();
        var body = list.Select(r => new[]
        {
            r.Date, r.DocType, r.DocNumber, r.Description, N(r.Debit), N(r.Credit), N(r.Balance)
        }).ToList();

        // ردیفِ جمع + ماندهٔ پایان.
        body.Add(new[] { "", "", "", "جمعِ گردش", N(totalDebit), N(totalCredit), "" });
        var label = closingBalance >= 0
            ? (isSupplier ? "ماندهٔ بدهیِ ما به تأمین‌کننده" : "ماندهٔ بدهکارِ مشتری")
            : (isSupplier ? "ماندهٔ طلبِ ما از تأمین‌کننده" : "ماندهٔ بستانکارِ مشتری");
        body.Add(new[] { "", "", "", label, "", "", N(Math.Abs(closingBalance)) });

        var title = (isSupplier ? "اظهارِ حسابِ تأمین‌کننده — " : "اظهارِ حسابِ مشتری — ") + partyName;
        return new ReportTable(title,
            new[] { "تاریخ", "نوعِ سند", "شماره", "شرح", "بدهکار", "بستانکار", "مانده" }, body);
    }

    private static string N(decimal v) =>
        Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture);
}
