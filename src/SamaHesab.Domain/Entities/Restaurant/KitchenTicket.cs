using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Restaurant;

/// <summary>رسید آشپزخانه (Kitchen Display System). هنگام ارسال سفارش به آشپزخانه ساخته می‌شود
/// و وضعیت آماده‌سازی را برای نمایشگر آشپزخانه نگه می‌دارد.</summary>
public class KitchenTicket : AuditableEntity
{
    public int BranchId { get; private set; }
    public int OrderId { get; private set; }
    public string TicketNumber { get; private set; } = default!;
    public string? TableName { get; private set; }
    public KitchenTicketStatus Status { get; private set; } = KitchenTicketStatus.New;
    public DateTime? ReadyAt { get; private set; }

    private KitchenTicket() { }

    public static KitchenTicket Create(int companyId, int branchId, int orderId,
        string ticketNumber, string? tableName)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
            throw new ArgumentException("شماره رسید آشپزخانه الزامی است.");
        return new KitchenTicket
        {
            CompanyId = companyId,
            BranchId = branchId,
            OrderId = orderId,
            TicketNumber = ticketNumber,
            TableName = tableName
        };
    }

    public void MarkPreparing() { Status = KitchenTicketStatus.Preparing; UpdatedAt = DateTime.Now; }

    public void MarkReady()
    {
        Status = KitchenTicketStatus.Ready;
        ReadyAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }

    public void Complete() { Status = KitchenTicketStatus.Completed; UpdatedAt = DateTime.Now; }
}
