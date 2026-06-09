using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Restaurant;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Restaurant.Commands;

/// <summary>
/// تغییر وضعیت رسید آشپزخانه (KDS) و آبشاری‌کردن آن به ردیف‌های سفارش:
/// New → Preparing (در حال آماده‌سازی) → Ready (آماده) → Completed (تحویل‌شده).
/// </summary>
public record AdvanceKitchenTicketCommand(int TicketId, KitchenTicketStatus Status) : IRequest<Result>;

public class AdvanceKitchenTicketCommandHandler : IRequestHandler<AdvanceKitchenTicketCommand, Result>
{
    private readonly IRepository<KitchenTicket> _tickets;
    private readonly IRepository<RestaurantOrderItem> _items;
    private readonly IUnitOfWork _uow;

    public AdvanceKitchenTicketCommandHandler(IRepository<KitchenTicket> tickets,
        IRepository<RestaurantOrderItem> items, IUnitOfWork uow)
    { _tickets = tickets; _items = items; _uow = uow; }

    public async Task<Result> Handle(AdvanceKitchenTicketCommand req, CancellationToken ct)
    {
        var ticket = await _tickets.GetByIdAsync(req.TicketId, ct);
        if (ticket is null) return Result.Failure("رسید آشپزخانه یافت نشد.");

        try
        {
            switch (req.Status)
            {
                case KitchenTicketStatus.Preparing: ticket.MarkPreparing(); break;
                case KitchenTicketStatus.Ready: ticket.MarkReady(); break;
                case KitchenTicketStatus.Completed: ticket.Complete(); break;
                default: return Result.Failure("وضعیت نامعتبر است.");
            }
            _tickets.Update(ticket);

            // آبشاری‌کردن وضعیت به ردیف‌های همان رسید
            var lines = await _items.FindAsync(i => i.KitchenTicketId == req.TicketId, ct);
            foreach (var i in lines)
            {
                switch (req.Status)
                {
                    case KitchenTicketStatus.Preparing: i.MarkPreparing(); break;
                    case KitchenTicketStatus.Ready: i.MarkReady(); break;
                    case KitchenTicketStatus.Completed: i.MarkServed(); break;
                }
                _items.Update(i);
            }

            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}
