using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// پروژه — بُعدِ تحلیلیِ سند (هستهٔ ERP). برای پیمانکاری/پروژه‌محور.
/// روی <see cref="VoucherItem.ProjectId"/> ثبت می‌شود و گزارش‌ها بر اساس آن تفکیک می‌شوند.
/// </summary>
public class Project : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? StartDate { get; private set; }   // شمسی yyyy/MM/dd
    public string? EndDate { get; private set; }
    public decimal Budget { get; private set; }
    public bool IsClosed { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Project() { }

    public static Project Create(int companyId, string code, string name,
        string? startDate = null, string? endDate = null, decimal budget = 0)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد پروژه الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام پروژه الزامی است.");
        return new Project
        {
            CompanyId = companyId, Code = code, Name = name,
            StartDate = startDate, EndDate = endDate, Budget = budget
        };
    }

    public void Update(string name, string? startDate, string? endDate, decimal budget)
    {
        Name = name; StartDate = startDate; EndDate = endDate; Budget = budget; SetAudit(null);
    }

    public void Close() { IsClosed = true; IsActive = false; SetAudit(null); }
    public void Reopen() { IsClosed = false; IsActive = true; SetAudit(null); }
    public void Deactivate() { IsActive = false; SetAudit(null); }
}
