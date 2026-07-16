using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.CRM;

/// <summary>
/// U-PARTY-LEDGER (backlog #9) — دفترِ معینِ طرف‌حساب: هر رویدادِ اثرگذار بر مانده (فاکتور/برگشت/
/// دریافت/پرداخت/تسویهٔ کنسینمنت/فروشِ گردشگری و ...) یک ردیفِ append-only و امضادار اینجا ثبت
/// می‌کند. <see cref="Party.Balance"/> کشِ سریع‌خوانی است؛ منبعِ حقیقت/محاسبه‌شده همین جمعِ
/// <see cref="Amount"/>هاست. Amount مثبت = افزایشِ بدهیِ طرف‌حساب به ما، منفی = کاهش.
/// </summary>
public class PartyLedgerEntry : AuditableEntity
{
    public int PartyId { get; private set; }
    public string Date { get; private set; } = default!;
    public string DocType { get; private set; } = default!;
    public string? DocNumber { get; private set; }
    public string? Description { get; private set; }
    public decimal Amount { get; private set; }

    private PartyLedgerEntry() { }

    public static PartyLedgerEntry Create(int companyId, int partyId, string date, string docType,
        string? docNumber, string? description, decimal amount)
    {
        if (partyId <= 0) throw new ArgumentException("طرف‌حساب الزامی است.");
        return new PartyLedgerEntry
        {
            CompanyId = companyId,
            PartyId = partyId,
            Date = date,
            DocType = docType,
            DocNumber = docNumber,
            Description = description,
            Amount = amount
        };
    }
}
