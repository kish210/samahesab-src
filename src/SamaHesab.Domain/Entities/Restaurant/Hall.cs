using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Restaurant;

/// <summary>سالن رستوران (مثلاً سالن اصلی، تراس، طبقه‌ی دوم). میزها داخل سالن قرار می‌گیرند.</summary>
public class Hall : AuditableEntity
{
    public int BranchId { get; private set; }
    public string Name { get; private set; } = default!;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<DiningTable> Tables { get; private set; } = new List<DiningTable>();

    private Hall() { }

    public static Hall Create(int companyId, int branchId, string name, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام سالن الزامی است.");
        return new Hall
        {
            CompanyId = companyId,
            BranchId = branchId,
            Name = name.Trim(),
            DisplayOrder = displayOrder
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام سالن الزامی است.");
        Name = name.Trim();
        UpdatedAt = DateTime.Now;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.Now; }
}
