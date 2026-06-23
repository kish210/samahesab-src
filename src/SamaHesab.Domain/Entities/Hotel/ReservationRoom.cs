using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Hotel;

/// <summary>PMS-C1-1 — خطِ اتاقِ رزرو. اتاقِ فیزیکی هنگام/نزدیکِ check-in تخصیص می‌یابد.</summary>
public class ReservationRoom : BaseEntity
{
    public int ReservationId { get; private set; }
    public int RoomTypeId { get; private set; }
    public int? RoomId { get; private set; }
    public string? RatePlanSnapshot { get; private set; }
    public decimal RatePerNight { get; private set; }
    public int ExtraBeds { get; private set; }

    private ReservationRoom() { }

    public static ReservationRoom Create(int roomTypeId, decimal ratePerNight, string? ratePlanSnapshot = null, int extraBeds = 0)
    {
        if (roomTypeId <= 0) throw new ArgumentException("نوعِ اتاق الزامی است.");
        return new ReservationRoom { RoomTypeId = roomTypeId, RatePerNight = ratePerNight < 0 ? 0 : ratePerNight,
            RatePlanSnapshot = ratePlanSnapshot, ExtraBeds = extraBeds < 0 ? 0 : extraBeds };
    }

    public void AssignRoom(int roomId) => RoomId = roomId;
}
