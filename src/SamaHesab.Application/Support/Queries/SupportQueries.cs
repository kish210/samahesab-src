using MediatR;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Support.Queries;

/// <summary>برچسب‌های فارسیِ وضعیت/همگام‌سازی (مشترکِ نماها).</summary>
public static class SupportLabels
{
    public static string Status(SupportStatus s) => s switch
    {
        SupportStatus.New => "جدید", SupportStatus.Open => "باز", SupportStatus.InProgress => "در حالِ بررسی",
        SupportStatus.WaitingCustomer => "منتظرِ مشتری", SupportStatus.Testing => "در حالِ آزمایش",
        SupportStatus.Resolved => "حل‌شده", SupportStatus.Closed => "بسته", _ => s.ToString()
    };

    public static string Sync(SyncState s) => s switch
    {
        SyncState.Local => "محلی", SyncState.Queued => "در صفِ ارسال",
        SyncState.Synced => "ارسال‌شده", SyncState.Failed => "ناموفق", _ => s.ToString()
    };
}

// ── «درخواست‌های من» — نمای یکپارچهٔ محلیِ باگ/قابلیت/تیکت ──
public record MyRequestItem(string Kind, int LocalId, string Title, string StatusText, string SyncText,
    DateTime CreatedAt, string? RemoteId);

public record GetMyRequestsQuery : IRequest<IReadOnlyList<MyRequestItem>>;

public class GetMyRequestsQueryHandler : IRequestHandler<GetMyRequestsQuery, IReadOnlyList<MyRequestItem>>
{
    private readonly IRepository<BugReport> _bugs;
    private readonly IRepository<FeatureRequest> _features;
    private readonly IRepository<SupportTicket> _tickets;

    public GetMyRequestsQueryHandler(IRepository<BugReport> bugs, IRepository<FeatureRequest> features,
        IRepository<SupportTicket> tickets)
    { _bugs = bugs; _features = features; _tickets = tickets; }

    public async Task<IReadOnlyList<MyRequestItem>> Handle(GetMyRequestsQuery req, CancellationToken ct)
    {
        var items = new List<MyRequestItem>();
        foreach (var b in await _bugs.GetAllAsync(ct))
            items.Add(new("باگ", b.Id, b.Title, SupportLabels.Status(b.Status), SupportLabels.Sync(b.Sync), b.CreatedAt, b.RemoteId));
        foreach (var f in await _features.GetAllAsync(ct))
            items.Add(new("قابلیت", f.Id, f.Title, SupportLabels.Status(f.Status), SupportLabels.Sync(f.Sync), f.CreatedAt, f.RemoteId));
        foreach (var t in await _tickets.GetAllAsync(ct))
            items.Add(new("تیکت", t.Id, t.Subject, SupportLabels.Status(t.Status), SupportLabels.Sync(t.Sync), t.CreatedAt, t.RemoteId));

        return items.OrderByDescending(i => i.CreatedAt).ToList();
    }
}

// ── فهرستِ تیکت‌ها (با پیام‌ها برای نمای گفت‌وگو) ──
public record TicketMessageDto(string Author, string Text, bool FromSupport, DateTime SentAt);
public record TicketDto(int Id, string Subject, string Body, string StatusText, string SyncText,
    DateTime CreatedAt, DateTime? LastActivityAt, IReadOnlyList<TicketMessageDto> Messages);

public record GetSupportTicketsQuery : IRequest<IReadOnlyList<TicketDto>>;

public class GetSupportTicketsQueryHandler : IRequestHandler<GetSupportTicketsQuery, IReadOnlyList<TicketDto>>
{
    private readonly IRepository<SupportTicket> _tickets;
    private readonly IRepository<TicketMessage> _messages;

    public GetSupportTicketsQueryHandler(IRepository<SupportTicket> tickets, IRepository<TicketMessage> messages)
    { _tickets = tickets; _messages = messages; }

    public async Task<IReadOnlyList<TicketDto>> Handle(GetSupportTicketsQuery req, CancellationToken ct)
    {
        var tickets = await _tickets.GetAllAsync(ct);
        var allMsgs = await _messages.GetAllAsync(ct);
        return tickets
            .OrderByDescending(t => t.LastActivityAt ?? t.CreatedAt)
            .Select(t => new TicketDto(t.Id, t.Subject, t.Body,
                SupportLabels.Status(t.Status), SupportLabels.Sync(t.Sync), t.CreatedAt, t.LastActivityAt,
                allMsgs.Where(m => m.TicketId == t.Id).OrderBy(m => m.SentAt)
                    .Select(m => new TicketMessageDto(m.Author, m.Text, m.FromSupport, m.SentAt)).ToList()))
            .ToList();
    }
}
