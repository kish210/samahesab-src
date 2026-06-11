using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Domain.Entities.Restaurant;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Restaurant.Commands;

// ─────────────────────────────────────────────────────────────────────────────
// باز کردن سفارش روی میز (یا بیرون‌بر/پیک)
// ─────────────────────────────────────────────────────────────────────────────
public record OpenTableCommand(
    RestaurantOrderType OrderType,
    int? TableId,
    int GuestCount = 1,
    int? WaiterId = null,
    int? CustomerId = null
) : IRequest<Result<int>>;

public class OpenTableCommandValidator : AbstractValidator<OpenTableCommand>
{
    public OpenTableCommandValidator()
    {
        RuleFor(x => x).Must(x => x.OrderType != RestaurantOrderType.DineIn || x.TableId is > 0)
            .WithMessage("برای سفارش سالن، انتخاب میز الزامی است.");
        RuleFor(x => x.GuestCount).GreaterThan(0).WithMessage("تعداد نفرات باید بزرگ‌تر از صفر باشد.");
    }
}

public class OpenTableCommandHandler : IRequestHandler<OpenTableCommand, Result<int>>
{
    private readonly IRestaurantOrderRepository _orders;
    private readonly IRepository<DiningTable> _tables;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public OpenTableCommandHandler(IRestaurantOrderRepository orders, IRepository<DiningTable> tables,
        IUnitOfWork uow, ICurrentUserService user)
    { _orders = orders; _tables = tables; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(OpenTableCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var branchId = _user.BranchId ?? 1;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            DiningTable? table = null;
            if (req.OrderType == RestaurantOrderType.DineIn)
            {
                table = await _tables.GetByIdAsync(req.TableId!.Value, ct);
                if (table is null) return Result<int>.Failure("میز یافت نشد.");
                if (table.Status == TableStatus.Occupied && table.CurrentOrderId is not null)
                    return Result<int>.Failure("این میز هم‌اکنون سفارش باز دارد.");
            }

            var number = await GenerateOrderNumberAsync(companyId, ct);
            var order = RestaurantOrder.Create(companyId, branchId, number, req.OrderType,
                req.TableId, req.GuestCount, req.WaiterId, req.CustomerId);
            await _orders.AddAsync(order, ct);
            await _uow.SaveChangesAsync(ct);   // get order.Id

            if (table is not null)
            {
                table.Occupy(order.Id);
                _tables.Update(table);
                await _uow.SaveChangesAsync(ct);
            }

            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(order.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }

    private async Task<string> GenerateOrderNumberAsync(int companyId, CancellationToken ct)
    {
        var count = await _orders.CountByCompanyAsync(companyId, ct);
        return $"S{(count + 1):D5}";
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// افزودن ردیف به سفارش باز
// ─────────────────────────────────────────────────────────────────────────────
public record AddOrderItemCommand(
    int OrderId,
    int ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount = 0,
    string? Notes = null
) : IRequest<Result>;

public class AddOrderItemCommandValidator : AbstractValidator<AddOrderItemCommand>
{
    public AddOrderItemCommandValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0);
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("کالا الزامی است.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("تعداد باید بزرگ‌تر از صفر باشد.");
    }
}

public class AddOrderItemCommandHandler : IRequestHandler<AddOrderItemCommand, Result>
{
    private readonly IRestaurantOrderRepository _orders;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public AddOrderItemCommandHandler(IRestaurantOrderRepository orders, IUnitOfWork uow, ICurrentUserService user)
    { _orders = orders; _uow = uow; _user = user; }

    public async Task<Result> Handle(AddOrderItemCommand req, CancellationToken ct)
    {
        var order = await _orders.GetWithItemsAsync(req.OrderId, ct);
        if (order is null) return Result.Failure("سفارش یافت نشد.");

        try
        {
            var item = RestaurantOrderItem.Create(_user.CompanyId ?? 1, req.ProductId, req.ProductName,
                req.Quantity, req.UnitPrice, req.DiscountAmount, req.Notes);
            order.AddItem(item);
            _orders.Update(order);
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ویرایش تعداد / حذف / یادداشت ردیف سفارش (#34)
// ─────────────────────────────────────────────────────────────────────────────
public record ChangeOrderItemQtyCommand(int OrderId, int ItemId, decimal Quantity) : IRequest<Result>;
public record RemoveOrderItemCommand(int OrderId, int ItemId) : IRequest<Result>;
public record SetOrderItemNotesCommand(int OrderId, int ItemId, string? Notes) : IRequest<Result>;

public class EditOrderItemCommandHandler :
    IRequestHandler<ChangeOrderItemQtyCommand, Result>,
    IRequestHandler<RemoveOrderItemCommand, Result>,
    IRequestHandler<SetOrderItemNotesCommand, Result>
{
    private readonly IRestaurantOrderRepository _orders;
    private readonly IUnitOfWork _uow;
    public EditOrderItemCommandHandler(IRestaurantOrderRepository orders, IUnitOfWork uow)
    { _orders = orders; _uow = uow; }

    public async Task<Result> Handle(ChangeOrderItemQtyCommand req, CancellationToken ct)
        => await Apply(req.OrderId, o => o.ChangeItemQuantity(req.ItemId, req.Quantity), ct);

    public async Task<Result> Handle(RemoveOrderItemCommand req, CancellationToken ct)
        => await Apply(req.OrderId, o => o.RemoveItem(req.ItemId), ct);

    public async Task<Result> Handle(SetOrderItemNotesCommand req, CancellationToken ct)
        => await Apply(req.OrderId, o => o.SetItemNotes(req.ItemId, req.Notes), ct);

    private async Task<Result> Apply(int orderId, Action<RestaurantOrder> change, CancellationToken ct)
    {
        var order = await _orders.GetWithItemsAsync(orderId, ct);
        if (order is null) return Result.Failure("سفارش یافت نشد.");
        try
        {
            change(order);
            _orders.Update(order);
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ارسال ردیف‌های در انتظار به آشپزخانه (ساخت رسید آشپزخانه)
// ─────────────────────────────────────────────────────────────────────────────
public record SendOrderToKitchenCommand(int OrderId) : IRequest<Result<int>>;

public class SendOrderToKitchenCommandHandler : IRequestHandler<SendOrderToKitchenCommand, Result<int>>
{
    private readonly IRestaurantOrderRepository _orders;
    private readonly IRepository<KitchenTicket> _tickets;
    private readonly IRepository<DiningTable> _tables;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SendOrderToKitchenCommandHandler(IRestaurantOrderRepository orders,
        IRepository<KitchenTicket> tickets, IRepository<DiningTable> tables,
        IUnitOfWork uow, ICurrentUserService user)
    { _orders = orders; _tickets = tickets; _tables = tables; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SendOrderToKitchenCommand req, CancellationToken ct)
    {
        var order = await _orders.GetWithItemsAsync(req.OrderId, ct);
        if (order is null) return Result<int>.Failure("سفارش یافت نشد.");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _user.CompanyId ?? 1;
            string? tableName = null;
            if (order.TableId is not null)
                tableName = (await _tables.GetByIdAsync(order.TableId.Value, ct))?.Name;

            var ticketNo = $"K{(await _tickets.CountAsync(t => t.CompanyId == companyId, ct) + 1):D5}";
            var ticket = KitchenTicket.Create(companyId, order.BranchId, order.Id, ticketNo, tableName);
            await _tickets.AddAsync(ticket, ct);
            await _uow.SaveChangesAsync(ct);   // get ticket.Id

            order.SendToKitchen(ticket.Id);    // throws if nothing pending; raises domain event
            _orders.Update(order);
            await _uow.SaveChangesAsync(ct);

            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(ticket.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// تسویه‌ی سفارش و آزادسازی میز
// ─────────────────────────────────────────────────────────────────────────────
public record SettleOrderCommand(
    int OrderId,
    decimal PaidAmount,
    decimal Discount = 0,
    decimal ServiceCharge = 0,
    decimal Tax = 0,
    decimal Tip = 0
) : IRequest<Result>;

public class SettleOrderCommandHandler : IRequestHandler<SettleOrderCommand, Result>
{
    private readonly IRestaurantOrderRepository _orders;
    private readonly IRepository<DiningTable> _tables;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public SettleOrderCommandHandler(IRestaurantOrderRepository orders, IRepository<DiningTable> tables,
        IUnitOfWork uow, ICurrentUserService user, IMediator mediator, IPersianCalendarService calendar)
    { _orders = orders; _tables = tables; _uow = uow; _user = user; _mediator = mediator; _calendar = calendar; }

    public async Task<Result> Handle(SettleOrderCommand req, CancellationToken ct)
    {
        var order = await _orders.GetWithItemsAsync(req.OrderId, ct);
        if (order is null) return Result.Failure("سفارش یافت نشد.");
        if (order.Status is RestaurantOrderStatus.Settled or RestaurantOrderStatus.Cancelled)
            return Result.Failure("سفارش قابل تسویه نیست.");

        // ── ۱) ثبت فروش از مسیر موجود فروش (فاکتور + کاهش موجودی + سند حسابداری خودکار) ──
        // service/tax/tip در «سایر هزینه‌ها» و تخفیف کل در InvoiceDiscount می‌رود تا جمع با سفارش یکی شود.
        int? invoiceId = null;
        var activeItems = order.Items.Where(i => i.Status != OrderItemStatus.Cancelled).ToList();
        if (activeItems.Count > 0)
        {
            var party = order.TableId is not null ? $" میز {order.TableId}" : $" ({order.OrderType})";
            var saleCmd = new CreateSalesInvoiceCommand(
                BranchId: _user.BranchId ?? 1, FiscalYearId: 1,
                InvoiceDate: _calendar.GetCurrentPersianDate(),
                CustomerId: order.CustomerId ?? 1, WarehouseId: 1,
                InvoiceType: InvoiceType.Sale, PriceLevel: "خرده",
                SalesRepId: null, DueDate: null,
                Description: $"رستوران — سفارش {order.OrderNumber}{party}",
                Shipping: 0, OtherCosts: req.ServiceCharge + req.Tax + req.Tip,
                Items: activeItems.Select(i => new SalesInvoiceItemDto(
                    i.ProductId, i.Quantity, i.UnitPrice, 0, 0, i.Notes, null, null)).ToList(),
                InvoiceDiscount: req.Discount,
                PaidAmount: req.PaidAmount,
                PaymentMethod: "نقدی");
            var saleResult = await _mediator.Send(saleCmd, ct);
            // اگر ثبت فروش/سند ناموفق بود، تسویه‌ی میز را متوقف نمی‌کنیم (میز باید آزاد شود)؛ صرفاً بدون لینک فاکتور.
            if (saleResult.Succeeded) invoiceId = saleResult.Value;
        }

        // ── ۲) تسویه‌ی سفارش + لینک فاکتور + آزادسازی میز (تراکنشی) ──
        await _uow.BeginTransactionAsync(ct);
        try
        {
            order.SetCharges(req.Discount, req.ServiceCharge, req.Tax, req.Tip);
            order.Settle(_user.UserId ?? 0, req.PaidAmount);   // raises RestaurantOrderSettledEvent
            if (invoiceId is not null) order.LinkSalesInvoice(invoiceId.Value);
            _orders.Update(order);

            if (order.TableId is not null)
            {
                var table = await _tables.GetByIdAsync(order.TableId.Value, ct);
                if (table is not null) { table.Free(); _tables.Update(table); }
            }

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result.Failure(ex.GetBaseException().Message);
        }
    }
}
