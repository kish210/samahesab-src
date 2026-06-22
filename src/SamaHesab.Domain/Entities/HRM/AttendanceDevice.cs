using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

/// <summary>ATTP-C1-3 — دستگاهِ ثبتِ تردد (کارت‌خوان/اثرانگشت). ترددِ خام به این دستگاه نسبت داده می‌شود.</summary>
public class AttendanceDevice : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Code { get; private set; }        // شناسه/سریالِ دستگاه
    public string? Location { get; private set; }
    public bool IsActive { get; private set; } = true;

    private AttendanceDevice() { }

    public static AttendanceDevice Create(int companyId, string name, string? code = null, string? location = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ دستگاه الزامی است.");
        return new AttendanceDevice { CompanyId = companyId, Name = name, Code = code, Location = location };
    }

    public void Update(string name, string? code, string? location, bool isActive)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        Code = code; Location = location; IsActive = isActive;
        SetAudit(null);
    }
}
