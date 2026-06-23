using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Hotel;

public enum RoomStatus { Vacant_Clean = 0, Vacant_Dirty = 1, Occupied_Clean = 2, Occupied_Dirty = 3, Inspected = 4, OutOfOrder = 5, Blocked = 6 }

/// <summary>PMS-C1-1 — اتاقِ فیزیکی + وضعیتِ هاوس‌کیپینگ/اشغال.</summary>
public class Room : AuditableEntity
{
    public int RoomTypeId { get; private set; }
    public string Number { get; private set; } = default!;
    public string? Floor { get; private set; }
    public RoomStatus Status { get; private set; } = RoomStatus.Vacant_Clean;
    public bool Active { get; private set; } = true;

    private Room() { }

    public static Room Create(int companyId, int roomTypeId, string number, string? floor = null)
    {
        if (roomTypeId <= 0) throw new ArgumentException("نوعِ اتاق الزامی است.");
        if (string.IsNullOrWhiteSpace(number)) throw new ArgumentException("شمارهٔ اتاق الزامی است.");
        return new Room { CompanyId = companyId, RoomTypeId = roomTypeId, Number = number, Floor = floor };
    }

    public void SetStatus(RoomStatus s) { Status = s; SetAudit(null); }

    public void Update(int roomTypeId, string number, string? floor, bool active)
    {
        if (roomTypeId > 0) RoomTypeId = roomTypeId;
        if (!string.IsNullOrWhiteSpace(number)) Number = number;
        Floor = floor; Active = active; SetAudit(null);
    }
}
