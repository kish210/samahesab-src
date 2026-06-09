using MediatR;
using SamaHesab.Domain.Entities.Restaurant;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Restaurant.Queries;

// ─────────────────────────────────────────────────────────────────────────────
// صفحه‌ی گارسون: سالن‌ها و میزها با وضعیت‌شان
// ─────────────────────────────────────────────────────────────────────────────
public record GetHallsWithTablesQuery() : IRequest<List<HallDto>>;

public record HallDto(int Id, string Name, int DisplayOrder, List<TableDto> Tables);
public record TableDto(int Id, int HallId, string Name, int Capacity, string Status,
    int StatusCode, int? CurrentOrderId, int PositionX, int PositionY);

public class GetHallsWithTablesQueryHandler : IRequestHandler<GetHallsWithTablesQuery, List<HallDto>>
{
    private readonly IRepository<Hall> _halls;
    private readonly IRepository<DiningTable> _tables;
    public GetHallsWithTablesQueryHandler(IRepository<Hall> halls, IRepository<DiningTable> tables)
    { _halls = halls; _tables = tables; }

    private static readonly Dictionary<TableStatus, string> StatusFa = new()
    {
        [TableStatus.Free] = "آزاد", [TableStatus.Occupied] = "مشغول",
        [TableStatus.Reserved] = "رزرو", [TableStatus.Billing] = "در حال تسویه"
    };

    public async Task<List<HallDto>> Handle(GetHallsWithTablesQuery request, CancellationToken ct)
    {
        var halls = await _halls.FindAsync(h => h.IsActive, ct);
        var tables = await _tables.FindAsync(t => t.IsActive, ct);
        return halls.OrderBy(h => h.DisplayOrder).Select(h => new HallDto(
            h.Id, h.Name, h.DisplayOrder,
            tables.Where(t => t.HallId == h.Id)
                  .OrderBy(t => t.Name)
                  .Select(t => new TableDto(t.Id, t.HallId, t.Name, t.Capacity,
                      StatusFa.GetValueOrDefault(t.Status, "?"), (int)t.Status,
                      t.CurrentOrderId, t.PositionX, t.PositionY))
                  .ToList()
        )).ToList();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// جزئیات یک سفارش (با ردیف‌ها)
// ─────────────────────────────────────────────────────────────────────────────
public record GetOrderQuery(int OrderId) : IRequest<OrderDto?>;

public record OrderDto(int Id, string OrderNumber, string OrderType, string Status,
    int? TableId, int GuestCount, decimal SubTotal, decimal Discount, decimal ServiceCharge,
    decimal Tax, decimal Tip, decimal GrandTotal, decimal PaidAmount, int? SalesInvoiceId, List<OrderItemDto> Items);
public record OrderItemDto(int Id, int ProductId, string ProductName, decimal Quantity,
    decimal UnitPrice, decimal DiscountAmount, decimal LineTotal, string Status, int StatusCode, string? Notes);

public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IRestaurantOrderRepository _orders;
    public GetOrderQueryHandler(IRestaurantOrderRepository orders) { _orders = orders; }

    private static readonly Dictionary<OrderItemStatus, string> ItemFa = new()
    {
        [OrderItemStatus.Pending] = "در انتظار", [OrderItemStatus.InKitchen] = "آشپزخانه",
        [OrderItemStatus.Preparing] = "در حال آماده‌سازی", [OrderItemStatus.Ready] = "آماده",
        [OrderItemStatus.Served] = "سرو شد", [OrderItemStatus.Cancelled] = "لغو"
    };
    private static readonly Dictionary<RestaurantOrderStatus, string> OrderFa = new()
    {
        [RestaurantOrderStatus.Open] = "باز", [RestaurantOrderStatus.Sent] = "ارسال‌شده",
        [RestaurantOrderStatus.Served] = "سرو شد", [RestaurantOrderStatus.Settled] = "تسویه",
        [RestaurantOrderStatus.Cancelled] = "لغو"
    };

    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken ct)
    {
        var o = await _orders.GetWithItemsAsync(request.OrderId, ct);
        if (o is null) return null;
        return new OrderDto(o.Id, o.OrderNumber, o.OrderType.ToString(),
            OrderFa.GetValueOrDefault(o.Status, "?"), o.TableId, o.GuestCount,
            o.SubTotal, o.Discount, o.ServiceCharge, o.Tax, o.Tip, o.GrandTotal, o.PaidAmount, o.SalesInvoiceId,
            o.Items.Select(i => new OrderItemDto(i.Id, i.ProductId, i.ProductName, i.Quantity,
                i.UnitPrice, i.DiscountAmount, i.LineTotal,
                ItemFa.GetValueOrDefault(i.Status, "?"), (int)i.Status, i.Notes)).ToList());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// نمایشگر آشپزخانه (KDS): رسیدهای فعال + آیتم‌هایشان
// ─────────────────────────────────────────────────────────────────────────────
public record GetKitchenBoardQuery() : IRequest<List<KitchenTicketDto>>;

public record KitchenTicketDto(int Id, string TicketNumber, int OrderId, string? TableName,
    string Status, int StatusCode, DateTime CreatedAt, List<KitchenItemDto> Items);
public record KitchenItemDto(int Id, string ProductName, decimal Quantity, string Status, string? Notes);

public class GetKitchenBoardQueryHandler : IRequestHandler<GetKitchenBoardQuery, List<KitchenTicketDto>>
{
    private readonly IRepository<KitchenTicket> _tickets;
    private readonly IRepository<RestaurantOrderItem> _items;
    public GetKitchenBoardQueryHandler(IRepository<KitchenTicket> tickets, IRepository<RestaurantOrderItem> items)
    { _tickets = tickets; _items = items; }

    private static readonly Dictionary<KitchenTicketStatus, string> TicketFa = new()
    {
        [KitchenTicketStatus.New] = "جدید", [KitchenTicketStatus.Preparing] = "در حال آماده‌سازی",
        [KitchenTicketStatus.Ready] = "آماده", [KitchenTicketStatus.Completed] = "تکمیل"
    };
    private static readonly Dictionary<OrderItemStatus, string> ItemFa = new()
    {
        [OrderItemStatus.InKitchen] = "آشپزخانه", [OrderItemStatus.Preparing] = "در حال آماده‌سازی",
        [OrderItemStatus.Ready] = "آماده", [OrderItemStatus.Served] = "سرو شد"
    };

    public async Task<List<KitchenTicketDto>> Handle(GetKitchenBoardQuery request, CancellationToken ct)
    {
        var active = await _tickets.FindAsync(t => t.Status != KitchenTicketStatus.Completed, ct);
        var result = new List<KitchenTicketDto>();
        foreach (var t in active.OrderBy(t => t.CreatedAt))
        {
            var items = await _items.FindAsync(i => i.KitchenTicketId == t.Id, ct);
            result.Add(new KitchenTicketDto(t.Id, t.TicketNumber, t.OrderId, t.TableName,
                TicketFa.GetValueOrDefault(t.Status, "?"), (int)t.Status, t.CreatedAt,
                items.Select(i => new KitchenItemDto(i.Id, i.ProductName, i.Quantity,
                    ItemFa.GetValueOrDefault(i.Status, "?"), i.Notes)).ToList()));
        }
        return result;
    }
}
