using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Support;

/// <summary>
/// 🆘 HC-2 — گزارشِ باگ. الصاقِ خودکارِ عکسِ تشخیصی (DiagnosticsJson) بدونِ هیچ دادهٔ مالی.
/// محلی ذخیره می‌شود و در صورتِ پیکربندیِ سرورِ پشتیبانی همگام (Sync) می‌شود.
/// </summary>
public class BugReport : AuditableEntity
{
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public BugSeverity Severity { get; private set; }
    public SupportCategory Category { get; private set; }
    public string? ExpectedResult { get; private set; }
    public string? ActualResult { get; private set; }
    public string? StepsToReproduce { get; private set; }

    /// <summary>عکسِ تشخیصیِ سیستم (JSON) — فقط دادهٔ فنی.</summary>
    public string? DiagnosticsJson { get; private set; }
    /// <summary>صفحه/ماژولِ جاری هنگامِ ثبت.</summary>
    public string? ScreenName { get; private set; }
    /// <summary>مسیرِ پیوست (اسکرین‌شات/لاگ) — اختیاری.</summary>
    public string? AttachmentPath { get; private set; }

    public SupportStatus Status { get; private set; } = SupportStatus.New;

    // ── همگام‌سازی با سرورِ پشتیبانی ──
    public SyncState Sync { get; private set; } = SyncState.Local;
    public string? RemoteId { get; private set; }   // شناسهٔ ثبت‌شده در وردپرس
    public DateTime? SyncedAt { get; private set; }

    private BugReport() { }

    public static BugReport Create(int companyId, string title, string description,
        BugSeverity severity, SupportCategory category,
        string? expected = null, string? actual = null, string? steps = null,
        string? diagnosticsJson = null, string? screenName = null, string? attachmentPath = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("عنوانِ گزارش الزامی است.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("شرحِ گزارش الزامی است.");
        return new BugReport
        {
            CompanyId = companyId,
            Title = title.Trim(),
            Description = description.Trim(),
            Severity = severity,
            Category = category,
            ExpectedResult = expected?.Trim(),
            ActualResult = actual?.Trim(),
            StepsToReproduce = steps?.Trim(),
            DiagnosticsJson = diagnosticsJson,
            ScreenName = screenName,
            AttachmentPath = attachmentPath,
        };
    }

    public void MarkQueued() => Sync = SyncState.Queued;

    public void MarkSynced(string remoteId)
    {
        RemoteId = remoteId;
        Sync = SyncState.Synced;
        SyncedAt = DateTime.Now;
    }

    public void MarkFailed() => Sync = SyncState.Failed;

    public void ChangeStatus(SupportStatus status) { Status = status; UpdatedAt = DateTime.Now; }
}
