using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

/// <summary>
/// ATT-C1-1 — شیفتِ کاری: ساعتِ شروع/پایان، استراحت، شیفتِ شب، ساعتِ موظفِ روزانه.
/// مبنای محاسبهٔ تأخیر/تعجیل/اضافه‌کاری/شب‌کاری در موتورِ تردد (ATT-C2-1) است.
/// </summary>
public class Shift : BaseEntity
{
    public int CompanyId { get; private set; }
    public string Name { get; private set; } = default!;       // نامِ شیفت (روزکار/شب‌کار/اداری)
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int BreakMinutes { get; private set; }              // استراحتِ بینِ شیفت (دقیقه)
    public bool IsNight { get; private set; }                  // شیفتِ شب (شاملِ بازهٔ ۲۲–۶)
    public decimal StandardHours { get; private set; } = 7.33m; // ساعتِ موظفِ روزانه (۴۴ ساعت/۶ روز)
    public bool IsActive { get; private set; } = true;
    public string? Notes { get; private set; }

    private Shift() { }

    public static Shift Create(int companyId, string name, TimeOnly start, TimeOnly end,
        int breakMinutes = 0, bool isNight = false, decimal standardHours = 7.33m)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ شیفت الزامی است.");
        return new Shift
        {
            CompanyId = companyId, Name = name, StartTime = start, EndTime = end,
            BreakMinutes = Math.Max(0, breakMinutes), IsNight = isNight,
            StandardHours = standardHours > 0 ? standardHours : 7.33m
        };
    }

    public void Update(string name, TimeOnly start, TimeOnly end, int breakMinutes,
        bool isNight, decimal standardHours, bool isActive, string? notes)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        StartTime = start; EndTime = end; BreakMinutes = Math.Max(0, breakMinutes);
        IsNight = isNight; StandardHours = standardHours > 0 ? standardHours : StandardHours;
        IsActive = isActive; Notes = notes;
    }

    public void Deactivate() => IsActive = false;
}
