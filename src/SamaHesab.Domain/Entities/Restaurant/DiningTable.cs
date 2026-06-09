using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Restaurant;

/// <summary>میز رستوران در یک سالن. وضعیت میز (آزاد/مشغول/رزرو/تسویه) مبنای صفحه‌ی گارسون است.</summary>
public class DiningTable : AuditableEntity
{
    public int BranchId { get; private set; }
    public int HallId { get; private set; }
    public string Name { get; private set; } = default!;   // شماره/نام میز، مثل «۱۲» یا «VIP-۲»
    public int Capacity { get; private set; } = 4;
    public TableStatus Status { get; private set; } = TableStatus.Free;
    public int? CurrentOrderId { get; private set; }       // سفارش باز روی میز (در صورت مشغول‌بودن)
    public int PositionX { get; private set; }             // برای چیدمان گرافیکی سالن
    public int PositionY { get; private set; }
    public bool IsActive { get; private set; } = true;

    private DiningTable() { }

    public static DiningTable Create(int companyId, int branchId, int hallId, string name,
        int capacity = 4, int positionX = 0, int positionY = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("نام/شماره میز الزامی است.");
        return new DiningTable
        {
            CompanyId = companyId,
            BranchId = branchId,
            HallId = hallId,
            Name = name.Trim(),
            Capacity = capacity < 1 ? 1 : capacity,
            PositionX = positionX,
            PositionY = positionY
        };
    }

    /// <summary>میز با باز شدن یک سفارش مشغول می‌شود.</summary>
    public void Occupy(int orderId)
    {
        if (Status == TableStatus.Occupied && CurrentOrderId is not null && CurrentOrderId != orderId)
            throw new InvalidOperationException("این میز هم‌اکنون سفارش باز دارد.");
        Status = TableStatus.Occupied;
        CurrentOrderId = orderId;
        UpdatedAt = DateTime.Now;
    }

    public void MarkBilling()
    {
        if (Status != TableStatus.Occupied)
            throw new InvalidOperationException("فقط میز مشغول قابل تسویه است.");
        Status = TableStatus.Billing;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>میز پس از تسویه آزاد می‌شود.</summary>
    public void Free()
    {
        Status = TableStatus.Free;
        CurrentOrderId = null;
        UpdatedAt = DateTime.Now;
    }

    public void Reserve()
    {
        if (Status == TableStatus.Occupied)
            throw new InvalidOperationException("میز مشغول را نمی‌توان رزرو کرد.");
        Status = TableStatus.Reserved;
        UpdatedAt = DateTime.Now;
    }

    public void Move(int x, int y) { PositionX = x; PositionY = y; UpdatedAt = DateTime.Now; }
    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.Now; }
}
