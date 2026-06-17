using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Support;

/// <summary>
/// 🆘 HC-2 — تیکتِ پشتیبانی با چرخهٔ وضعیت (New→Open→InProgress→WaitingCustomer→Testing→Resolved→Closed).
/// گفت‌وگو در <see cref="Messages"/> نگه‌داری می‌شود (الصاق در HC-4).
/// </summary>
public class SupportTicket : AuditableEntity
{
    public string Subject { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public SupportCategory Category { get; private set; }
    public SupportStatus Status { get; private set; } = SupportStatus.New;
    public DateTime? LastActivityAt { get; private set; }

    public SyncState Sync { get; private set; } = SyncState.Local;
    public string? RemoteId { get; private set; }
    public DateTime? SyncedAt { get; private set; }

    private readonly List<TicketMessage> _messages = new();
    public IReadOnlyCollection<TicketMessage> Messages => _messages.AsReadOnly();

    private SupportTicket() { }

    public static SupportTicket Create(int companyId, string subject, string body, SupportCategory category)
    {
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("موضوعِ تیکت الزامی است.");
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("متنِ تیکت الزامی است.");
        return new SupportTicket
        {
            CompanyId = companyId,
            Subject = subject.Trim(),
            Body = body.Trim(),
            Category = category,
            LastActivityAt = DateTime.Now,
        };
    }

    public TicketMessage AddMessage(string author, string text, bool fromSupport)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("متنِ پیام الزامی است.");
        var m = TicketMessage.Create(author, text, fromSupport);
        _messages.Add(m);
        LastActivityAt = DateTime.Now;
        if (fromSupport && Status == SupportStatus.WaitingCustomer) { /* منتظرِ مشتری بود */ }
        return m;
    }

    public void ChangeStatus(SupportStatus status) { Status = status; LastActivityAt = DateTime.Now; UpdatedAt = DateTime.Now; }

    public void MarkQueued() => Sync = SyncState.Queued;
    public void MarkSynced(string remoteId) { RemoteId = remoteId; Sync = SyncState.Synced; SyncedAt = DateTime.Now; }
    public void MarkFailed() => Sync = SyncState.Failed;
}

/// <summary>🆘 HC-2 — یک پیام در گفت‌وگوی تیکت.</summary>
public class TicketMessage : BaseEntity
{
    public int TicketId { get; private set; }
    public string Author { get; private set; } = default!;
    public string Text { get; private set; } = default!;
    public bool FromSupport { get; private set; }     // true = پاسخِ کارشناس
    public DateTime SentAt { get; private set; } = DateTime.Now;
    public string? AttachmentPath { get; private set; }

    private TicketMessage() { }

    public static TicketMessage Create(string author, string text, bool fromSupport, string? attachmentPath = null)
        => new()
        {
            Author = string.IsNullOrWhiteSpace(author) ? "—" : author.Trim(),
            Text = text.Trim(),
            FromSupport = fromSupport,
            AttachmentPath = attachmentPath,
        };
}
