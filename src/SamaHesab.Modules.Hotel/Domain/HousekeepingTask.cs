using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Hotel.Domain;

public enum HousekeepingType { DailyClean = 0, CheckoutClean = 1, Inspection = 2, Maintenance = 3 }
public enum HousekeepingTaskStatus { Pending = 0, InProgress = 1, Done = 2, Failed = 3 }
public enum MaintenanceStatus { Open = 0, InProgress = 1, Resolved = 2 }

/// <summary>PMS-C1-1 — کارِ هاوس‌کیپینگِ یک اتاق (نظافتِ روزانه/خروج/بازرسی/تعمیر).</summary>
public class HousekeepingTask : AuditableEntity
{
    public int RoomId { get; private set; }
    public HousekeepingType Type { get; private set; }
    public string Date { get; private set; } = default!;   // شمسی
    public HousekeepingTaskStatus Status { get; private set; } = HousekeepingTaskStatus.Pending;
    public int? AssignedToUserId { get; private set; }
    public string? Notes { get; private set; }

    private HousekeepingTask() { }

    public static HousekeepingTask Create(int companyId, int roomId, HousekeepingType type, string date, int? assignedToUserId = null)
    {
        if (roomId <= 0) throw new ArgumentException("اتاق الزامی است.");
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        return new HousekeepingTask { CompanyId = companyId, RoomId = roomId, Type = type, Date = date, AssignedToUserId = assignedToUserId };
    }

    public void SetStatus(HousekeepingTaskStatus s, string? notes = null)
    {
        Status = s;
        if (notes != null) Notes = notes;
        SetAudit(null);
    }
}

/// <summary>PMS-C1-1 — تیکتِ تعمیرات؛ در حالتِ بازِ مسدودکننده، اتاق را از فروش خارج می‌کند.</summary>
public class MaintenanceTicket : AuditableEntity
{
    public int RoomId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public string OpenDate { get; private set; } = default!;   // شمسی
    public string? ResolveDate { get; private set; }
    public MaintenanceStatus Status { get; private set; } = MaintenanceStatus.Open;
    public bool BlocksRoom { get; private set; }

    private MaintenanceTicket() { }

    public static MaintenanceTicket Create(int companyId, int roomId, string title, string openDate, bool blocksRoom = false, string? description = null)
    {
        if (roomId <= 0) throw new ArgumentException("اتاق الزامی است.");
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("عنوان الزامی است.");
        if (string.IsNullOrWhiteSpace(openDate)) throw new ArgumentException("تاریخ الزامی است.");
        return new MaintenanceTicket { CompanyId = companyId, RoomId = roomId, Title = title, OpenDate = openDate, BlocksRoom = blocksRoom, Description = description };
    }

    public void SetStatus(MaintenanceStatus s, string? resolveDate = null)
    {
        Status = s;
        if (s == MaintenanceStatus.Resolved) { ResolveDate = resolveDate; BlocksRoom = false; }
        SetAudit(null);
    }
}
