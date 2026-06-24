using System.Globalization;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Application.Documents;

/// <summary>
/// DT-6 — تأمینِ دادهٔ اسنادِ مالی (سند حسابداری/چک) برای موتورِ قالب (DocumentTemplateEngine).
/// منطقِ خالص و تست‌پذیر؛ بدونِ I/O. نامِ حساب/طرف از طریقِ lookup داده می‌شود (هسته به DB وابسته نمی‌شود).
/// توکن‌های اسکالر در Fields و ردیف‌ها در Rows قرار می‌گیرند (مطابقِ {Token} و [[ROWS]]).
/// </summary>
public static class AccountingDocumentData
{
    private static string M(decimal v) => v.ToString("#,0", CultureInfo.InvariantCulture);

    /// <summary>سندِ حسابداری → دادهٔ قالب. توکن‌ها: VoucherNumber/VoucherDate/Description/Reference/TotalDebit/TotalCredit؛
    /// ردیف‌ها: AccountCode?(نامِ حساب از lookup)/AccountName/Debit/Credit/Description.</summary>
    public static DocumentData Voucher(Voucher v, Func<int, string?> accountName)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["VoucherNumber"] = v.VoucherNumber,
            ["VoucherDate"]   = v.VoucherDate,
            ["Description"]   = v.Description ?? "",
            ["Reference"]     = v.Reference ?? "",
            ["TotalDebit"]    = M(v.TotalDebit),
            ["TotalCredit"]   = M(v.TotalCredit),
            ["RowCount"]      = v.Items.Count.ToString(CultureInfo.InvariantCulture),
        };
        var rows = v.Items
            .OrderBy(i => i.RowNumber)
            .Select(i => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["AccountName"] = accountName(i.AccountId) ?? $"#{i.AccountId}",
                ["Debit"]       = i.Debit == 0 ? "" : M(i.Debit),
                ["Credit"]      = i.Credit == 0 ? "" : M(i.Credit),
                ["Description"] = i.Description ?? "",
            })
            .ToList();
        return DocumentData.Of(fields, rows);
    }

    /// <summary>چک → دادهٔ قالب. توکن‌ها: ChequeNumber/BankName/Amount/DueDate/IssueDate/Type/PartyName/Status/IssuedBy.</summary>
    public static DocumentData Cheque(Cheque c, string? partyName)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ChequeNumber"] = c.ChequeNumber,
            ["BankName"]     = c.BankName,
            ["Amount"]       = M(c.Amount),
            ["DueDate"]      = c.DueDate,
            ["IssueDate"]    = c.IssueDate ?? "",
            ["Type"]         = c.ChequeType == ChequeType.Received ? "دریافتی" : "پرداختی",
            ["PartyName"]    = partyName ?? "",
            ["IssuedBy"]     = c.IssuedBy ?? "",
            ["Description"]  = c.Description ?? "",
            ["Status"]       = c.Status switch
            {
                ChequeStatus.InProcess => "در جریان",
                ChequeStatus.Cleared   => "وصول‌شده",
                ChequeStatus.Returned  => "برگشتی",
                _                      => c.Status.ToString(),
            },
        };
        return DocumentData.Of(fields);
    }
}
