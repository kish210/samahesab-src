using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Inventory;

public class Warehouse : AuditableEntity
{
    public int? BranchId { get; private set; }
    /// <summary>U-INV-ACCT-WH — حسابِ GLِ موجودیِ اختصاصیِ این انبار؛ null = حسابِ مشترکِ پیش‌فرضِ شرکت (1-05-001).</summary>
    public int? InventoryAccountId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Address { get; private set; }
    public string? ManagerName { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<StockItem> StockItems { get; private set; } = new List<StockItem>();

    private Warehouse() { }

    public static Warehouse Create(int companyId, string code, string name, int? branchId = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد انبار الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام انبار الزامی است.");

        return new Warehouse
        {
            CompanyId = companyId,
            Code = code,
            Name = name,
            BranchId = branchId
        };
    }

    public void Update(string name, string? address, string? managerName)
    {
        Name = name;
        Address = address;
        ManagerName = managerName;
        UpdatedAt = DateTime.Now;
    }

    public void SetAsDefault() { IsDefault = true; UpdatedAt = DateTime.Now; }
    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.Now; }
    public void SetInventoryAccount(int? accountId) { InventoryAccountId = accountId; UpdatedAt = DateTime.Now; }
}
