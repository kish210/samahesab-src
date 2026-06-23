using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Hotel;

/// <summary>PMS-C1-1 — نوعِ اتاق (سوئیت/دوتخته/...).</summary>
public class RoomType : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public int BaseCapacity { get; private set; } = 2;
    public bool ExtraBedAllowed { get; private set; }
    public bool Active { get; private set; } = true;

    private RoomType() { }

    public static RoomType Create(int companyId, string code, string name, int baseCapacity = 2, bool extraBedAllowed = false)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کدِ نوعِ اتاق الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ نوعِ اتاق الزامی است.");
        return new RoomType { CompanyId = companyId, Code = code, Name = name,
            BaseCapacity = baseCapacity < 1 ? 1 : baseCapacity, ExtraBedAllowed = extraBedAllowed };
    }

    public void Update(string name, int baseCapacity, bool extraBedAllowed, bool active)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        BaseCapacity = baseCapacity < 1 ? 1 : baseCapacity;
        ExtraBedAllowed = extraBedAllowed; Active = active; SetAudit(null);
    }
}
