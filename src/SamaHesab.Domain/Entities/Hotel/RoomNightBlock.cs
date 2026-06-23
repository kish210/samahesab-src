using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Hotel;

/// <summary>PMS-C1-1 — اتاق-شبِ رزروشده. UNIQUE(RoomId, Date) در DB → جلوگیری از رزروِ هم‌زمانِ یک اتاق-شب.</summary>
public class RoomNightBlock : AuditableEntity
{
    public int ReservationRoomId { get; private set; }
    public int RoomId { get; private set; }
    public string Date { get; private set; } = default!;   // شمسی

    private RoomNightBlock() { }

    public static RoomNightBlock Create(int companyId, int reservationRoomId, int roomId, string date)
    {
        if (roomId <= 0) throw new ArgumentException("اتاق الزامی است.");
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        return new RoomNightBlock { CompanyId = companyId, ReservationRoomId = reservationRoomId, RoomId = roomId, Date = date };
    }
}
