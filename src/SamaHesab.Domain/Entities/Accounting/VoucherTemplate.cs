using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// الگوی سند (Voucher Template): یک سند نمونه با ردیف‌های ازپیش‌تعریف‌شده (مثل اجاره، حقوق، قبوض).
/// کاربر از روی الگو یک سند پیش‌نویس می‌سازد و فقط مبلغ/تاریخ را تنظیم می‌کند — کلید «سند < ۳۰ ثانیه».
/// </summary>
public class VoucherTemplate : AuditableEntity
{
    public int BranchId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public int VoucherTypeId { get; private set; } = 1;
    public bool IsActive { get; private set; } = true;

    public ICollection<VoucherTemplateLine> Lines { get; private set; } = new List<VoucherTemplateLine>();

    private VoucherTemplate() { }

    public static VoucherTemplate Create(int companyId, int branchId, string name,
        int voucherTypeId = 1, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام الگو الزامی است.");
        return new VoucherTemplate
        {
            CompanyId = companyId,
            BranchId = branchId,
            Name = name.Trim(),
            VoucherTypeId = voucherTypeId,
            Description = description
        };
    }

    public void AddLine(VoucherTemplateLine line) => Lines.Add(line);

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.Now; }

    /// <summary>مجموع پیش‌فرض بدهکار/بستانکار (ممکن است صفر باشد اگر مبالغ در الگو تعریف نشده باشند).</summary>
    public decimal TotalDebit => Lines.Sum(l => l.Debit);
    public decimal TotalCredit => Lines.Sum(l => l.Credit);
}
