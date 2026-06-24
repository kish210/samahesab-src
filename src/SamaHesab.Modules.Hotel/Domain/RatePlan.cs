using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Hotel.Domain;

/// <summary>PMS-C1-1 — پلنِ نرخِ نوعِ اتاق در بازهٔ تاریخیِ شمسی (+ شارژِ آخرِ‌هفته/تعطیل، صبحانه).</summary>
public class RatePlan : AuditableEntity
{
    public int RoomTypeId { get; private set; }
    public string ValidFrom { get; private set; } = default!;   // شمسی
    public string ValidTo { get; private set; } = default!;
    public decimal NightlyRate { get; private set; }
    public decimal WeekendSurcharge { get; private set; }
    public decimal HolidaySurcharge { get; private set; }
    public bool IncludesBreakfast { get; private set; }
    public bool Active { get; private set; } = true;

    private RatePlan() { }

    public static RatePlan Create(int companyId, int roomTypeId, string validFrom, string validTo,
        decimal nightlyRate, decimal weekendSurcharge = 0, decimal holidaySurcharge = 0, bool includesBreakfast = false)
    {
        if (roomTypeId <= 0) throw new ArgumentException("نوعِ اتاق الزامی است.");
        if (nightlyRate < 0) throw new ArgumentException("نرخِ شب نمی‌تواند منفی باشد.");
        return new RatePlan { CompanyId = companyId, RoomTypeId = roomTypeId, ValidFrom = validFrom, ValidTo = validTo,
            NightlyRate = nightlyRate, WeekendSurcharge = weekendSurcharge, HolidaySurcharge = holidaySurcharge,
            IncludesBreakfast = includesBreakfast };
    }

    public void Update(string validFrom, string validTo, decimal nightlyRate, decimal weekendSurcharge,
        decimal holidaySurcharge, bool includesBreakfast, bool active)
    {
        ValidFrom = validFrom; ValidTo = validTo; NightlyRate = nightlyRate < 0 ? 0 : nightlyRate;
        WeekendSurcharge = weekendSurcharge; HolidaySurcharge = holidaySurcharge;
        IncludesBreakfast = includesBreakfast; Active = active; SetAudit(null);
    }
}
