using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// مرکز هزینه — بُعدِ تحلیلیِ سند (هستهٔ ERP). سلسله‌مراتبی (والد اختیاری).
/// روی <see cref="VoucherItem.CostCenterId"/> ثبت می‌شود و گزارش‌ها بر اساس آن تفکیک می‌شوند.
/// </summary>
public class CostCenter : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int? ParentId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private CostCenter() { }

    public static CostCenter Create(int companyId, string code, string name, int? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد مرکز هزینه الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام مرکز هزینه الزامی است.");
        return new CostCenter { CompanyId = companyId, Code = code, Name = name, ParentId = parentId };
    }

    public void Update(string name, int? parentId)
    {
        Name = name; ParentId = parentId; SetAudit(null);
    }

    public void Deactivate() { IsActive = false; SetAudit(null); }
    public void Activate() { IsActive = true; SetAudit(null); }
}
