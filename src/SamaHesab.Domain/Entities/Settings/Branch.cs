using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Settings;

public class Branch : BaseEntity
{
    public int CompanyId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? ManagerName { get; private set; }
    public bool IsHQ { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; private set; }

    public Company? Company { get; private set; }

    private Branch() { }

    public static Branch Create(int companyId, string code, string name, bool isHQ = false)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد شعبه الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام شعبه الزامی است.");

        return new Branch
        {
            CompanyId = companyId,
            Code = code,
            Name = name,
            IsHQ = isHQ
        };
    }

    public void Update(string name, string? address, string? phone, string? managerName)
    {
        Name = name;
        Address = address;
        Phone = phone;
        ManagerName = managerName;
        UpdatedAt = DateTime.Now;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.Now; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.Now; }
}
