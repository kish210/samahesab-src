using MediatR;
using SamaHesab.Application.Common.Events;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Events;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.EventHandlers;

/// <summary>
/// U-ACCT-2 (سامانهٔ مودیان) — قلابِ خودکارِ صف‌بندی: هر بار فاکتورِ فروش قطعی می‌شود
/// (<see cref="SalesInvoicePostedEvent"/>، از قبل در <c>SalesInvoice.Post</c> منتشر می‌شود)،
/// یک رکوردِ Pending ساخته می‌شود — بدونِ ارسالِ همزمان/شبکه‌ای (ارسالِ واقعی وظیفهٔ
/// <see cref="Commands.SendElectronicInvoiceCommand"/> است، خودکار یا دستی). اگر ماژول نصب/فعال
/// نباشد، این هندلر اصلاً ثبت نمی‌شود (RegisterServices صدا زده نمی‌شود) — هستهٔ فروش بدونِ اطلاع
/// از این ماژول کار می‌کند.
/// </summary>
public sealed class QueueElectronicInvoiceOnSalesPostedHandler
    : INotificationHandler<DomainEventNotification<SalesInvoicePostedEvent>>
{
    private readonly IRepository<ElectronicInvoiceSubmission> _submissions;
    private readonly IRepository<ModianSettings> _settings;
    private readonly IUnitOfWork _uow;

    public QueueElectronicInvoiceOnSalesPostedHandler(
        IRepository<ElectronicInvoiceSubmission> submissions, IRepository<ModianSettings> settings, IUnitOfWork uow)
    { _submissions = submissions; _settings = settings; _uow = uow; }

    public async Task Handle(DomainEventNotification<SalesInvoicePostedEvent> n, CancellationToken ct)
    {
        var settings = await _settings.FindSingleAsync(s => s.CompanyId == n.Event.CompanyId, ct);
        if (settings is null || !settings.Enabled) return;   // ماژول نصب است ولی فعال/پیکربندی‌نشده — بی‌صدا رد شو

        var already = await _submissions.AnyAsync(
            s => s.CompanyId == n.Event.CompanyId && s.SalesInvoiceId == n.Event.InvoiceId, ct);
        if (already) return;   // idempotent — این هندلر نباید دوباره صف کند اگر (نظری) دوبار فراخوانی شد

        await _submissions.AddAsync(ElectronicInvoiceSubmission.Create(n.Event.CompanyId, n.Event.InvoiceId), ct);
        await _uow.SaveChangesAsync(ct);
    }
}
