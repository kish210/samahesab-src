using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Security.Queries;

/// <summary>
/// T20 — مشاهدهٔ ردِّ حسابرسی (audit trail). ردیف‌های `Sec.AuditLogs` در یک بازهٔ زمانی،
/// با فیلترِ اختیاریِ «عمل»، جدیدترین اول و محدود به <paramref name="MaxRows"/>.
/// فیلترِ تاریخ در سطحِ DB اعمال می‌شود تا کلِ جدول بارگذاری نشود.
/// </summary>
public record GetAuditLogQuery(int DaysBack = 30, string? Action = null, int MaxRows = 500)
    : IRequest<List<AuditLogDto>>;

public record AuditLogDto(long Id, string When, string? User, string Action, string? TableName, string? Details);

public class GetAuditLogQueryHandler : IRequestHandler<GetAuditLogQuery, List<AuditLogDto>>
{
    private readonly IRepository<AuditLog> _audit;
    private readonly IPersianCalendarService _calendar;

    public GetAuditLogQueryHandler(IRepository<AuditLog> audit, IPersianCalendarService calendar)
    { _audit = audit; _calendar = calendar; }

    public async Task<List<AuditLogDto>> Handle(GetAuditLogQuery req, CancellationToken ct)
    {
        var days = req.DaysBack <= 0 ? 30 : req.DaysBack;
        var since = DateTime.Now.AddDays(-days);
        var rows = await _audit.FindAsync(a => a.CreatedAt >= since, ct);

        return rows
            .Where(a => string.IsNullOrWhiteSpace(req.Action) || a.Action == req.Action)
            .OrderByDescending(a => a.CreatedAt)
            .Take(req.MaxRows <= 0 ? 500 : req.MaxRows)
            .Select(a => new AuditLogDto(
                a.Id,
                $"{_calendar.ToPersianDate(a.CreatedAt)} {a.CreatedAt:HH:mm}",
                a.Username,
                a.Action,
                a.TableName,
                a.NewValues))
            .ToList();
    }
}
