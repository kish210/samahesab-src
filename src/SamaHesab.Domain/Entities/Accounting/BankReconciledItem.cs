using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// ردیفِ تطبیق‌شدهٔ مغایرت‌گیری بانکی (U-BANK-RECON-WEB) — ماندگاریِ دیتابیسی به‌جای
/// فایلِ محلیِ دسکتاپ (bank-recon.json). به‌ازای هر حسابِ بانکی، شناسهٔ ردیف‌های دفتر که
/// با صورت‌حسابِ بانک تطبیق و ثبت شده‌اند ذخیره می‌شود تا در مغایرت‌گیری‌های بعدی
/// دوباره نمایش داده نشوند.
/// </summary>
public class BankReconciledItem : AuditableEntity
{
    public int BankAccountId { get; private set; }
    public int VoucherItemId { get; private set; }
    public string ReconciledDate { get; private set; } = default!;  // شمسی «yyyy/MM/dd»

    private BankReconciledItem() { }

    public static BankReconciledItem Create(int companyId, int bankAccountId, int voucherItemId, string date)
    {
        if (voucherItemId <= 0)
            throw new ArgumentException("ردیف دفتر نامعتبر است.");
        if (string.IsNullOrWhiteSpace(date))
            throw new ArgumentException("تاریخ تطبیق الزامی است.");

        return new BankReconciledItem
        {
            CompanyId = companyId,
            BankAccountId = bankAccountId,
            VoucherItemId = voucherItemId,
            ReconciledDate = date.Trim()
        };
    }
}
