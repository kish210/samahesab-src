using MediatR;
using Microsoft.Extensions.Logging;
using SamaHesab.Application.Common.Events;
using SamaHesab.Domain.Events;

namespace SamaHesab.Application.Accounting.EventHandlers;

/// <summary>
/// نمونه‌ی هندلر رویداد دامنه (Phase 0): اکنون رویدادها زنده‌اند و منتشر می‌شوند.
/// فعلاً فقط ثبت ممیزی (لاگ) انجام می‌شود؛ در فازهای بعد می‌توان اعلان/اتوماسیون افزود.
/// </summary>
public sealed class VoucherPostedLogHandler : INotificationHandler<DomainEventNotification<VoucherPostedEvent>>
{
    private readonly ILogger<VoucherPostedLogHandler> _log;
    public VoucherPostedLogHandler(ILogger<VoucherPostedLogHandler> log) => _log = log;

    public Task Handle(DomainEventNotification<VoucherPostedEvent> n, CancellationToken ct)
    {
        _log.LogInformation("[رویداد] سند حسابداری #{VoucherId} قطعی شد (شرکت {CompanyId}، کاربر {UserId}).",
            n.Event.VoucherId, n.Event.CompanyId, n.Event.UserId);
        return Task.CompletedTask;
    }
}

public sealed class SalesInvoicePostedLogHandler : INotificationHandler<DomainEventNotification<SalesInvoicePostedEvent>>
{
    private readonly ILogger<SalesInvoicePostedLogHandler> _log;
    public SalesInvoicePostedLogHandler(ILogger<SalesInvoicePostedLogHandler> log) => _log = log;

    public Task Handle(DomainEventNotification<SalesInvoicePostedEvent> n, CancellationToken ct)
    {
        _log.LogInformation("[رویداد] فاکتور فروش #{InvoiceId} قطعی شد — مبلغ {Amount:N0} (مشتری {CustomerId}).",
            n.Event.InvoiceId, n.Event.Amount, n.Event.CustomerId);
        return Task.CompletedTask;
    }
}

public sealed class RestaurantOrderSettledLogHandler : INotificationHandler<DomainEventNotification<RestaurantOrderSettledEvent>>
{
    private readonly ILogger<RestaurantOrderSettledLogHandler> _log;
    public RestaurantOrderSettledLogHandler(ILogger<RestaurantOrderSettledLogHandler> log) => _log = log;

    public Task Handle(DomainEventNotification<RestaurantOrderSettledEvent> n, CancellationToken ct)
    {
        _log.LogInformation("[رویداد] سفارش رستوران #{OrderId} تسویه شد — مبلغ {Total:N0} (شعبه {BranchId}).",
            n.Event.OrderId, n.Event.GrandTotal, n.Event.BranchId);
        return Task.CompletedTask;
    }
}
