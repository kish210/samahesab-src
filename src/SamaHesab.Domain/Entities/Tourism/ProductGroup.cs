using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Tourism;

/// <summary>TUR-C1-1 — گروهِ خدمت/محصولِ گردشگری (مثلِ «بلیط»، «تور»، «گشت»).</summary>
public class ProductGroup : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public bool Active { get; private set; } = true;

    private ProductGroup() { }

    public static ProductGroup Create(int companyId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ گروه الزامی است.");
        return new ProductGroup { CompanyId = companyId, Name = name };
    }

    public void Update(string name, bool active)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        Active = active;
        SetAudit(null);
    }
}
