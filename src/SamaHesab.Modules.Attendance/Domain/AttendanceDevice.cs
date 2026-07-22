using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

/// <summary>ATTP-C1-3 — دستگاهِ ثبتِ تردد (کارت‌خوان/اثرانگشت). ترددِ خام به این دستگاه نسبت داده می‌شود.</summary>
public class AttendanceDevice : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public string? Code { get; private set; }        // شناسه/سریالِ دستگاه
    public string? Location { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>آدرسِ شبکه‌ایِ دستگاهِ زدکتکو (TCP/IP، پورتِ پیش‌فرض ۴۳۷۰) برایِ همگام‌سازیِ خودکارِ تردد.</summary>
    public string? IpAddress { get; private set; }
    public int Port { get; private set; } = 4370;
    /// <summary>رمزِ ارتباطیِ دستگاه (CommKey، پیش‌فرضِ کارخانه معمولاً «0»).</summary>
    public string? CommKey { get; private set; }

    private AttendanceDevice() { }

    public static AttendanceDevice Create(int companyId, string name, string? code = null, string? location = null,
        string? ipAddress = null, int port = 4370, string? commKey = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ دستگاه الزامی است.");
        return new AttendanceDevice
        {
            CompanyId = companyId, Name = name, Code = code, Location = location,
            IpAddress = ipAddress, Port = port > 0 ? port : 4370, CommKey = commKey,
        };
    }

    public void Update(string name, string? code, string? location, bool isActive,
        string? ipAddress = null, int port = 4370, string? commKey = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        Code = code; Location = location; IsActive = isActive;
        IpAddress = ipAddress; Port = port > 0 ? port : 4370; CommKey = commKey;
        SetAudit(null);
    }
}
