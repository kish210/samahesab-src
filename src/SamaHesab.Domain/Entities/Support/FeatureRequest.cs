using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Support;

/// <summary>🆘 HC-2 — درخواستِ قابلیتِ تازه از مشتری.</summary>
public class FeatureRequest : AuditableEntity
{
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string? BusinessBenefit { get; private set; }
    public FeaturePriority Priority { get; private set; } = FeaturePriority.Medium;
    public string? AttachmentPath { get; private set; }

    public SupportStatus Status { get; private set; } = SupportStatus.New;
    public int VoteCount { get; private set; }   // از وردپرس همگام می‌شود

    public SyncState Sync { get; private set; } = SyncState.Local;
    public string? RemoteId { get; private set; }
    public DateTime? SyncedAt { get; private set; }

    private FeatureRequest() { }

    public static FeatureRequest Create(int companyId, string title, string description,
        string? businessBenefit = null, FeaturePriority priority = FeaturePriority.Medium,
        string? attachmentPath = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("عنوانِ درخواست الزامی است.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("شرحِ درخواست الزامی است.");
        return new FeatureRequest
        {
            CompanyId = companyId,
            Title = title.Trim(),
            Description = description.Trim(),
            BusinessBenefit = businessBenefit?.Trim(),
            Priority = priority,
            AttachmentPath = attachmentPath,
        };
    }

    public void MarkQueued() => Sync = SyncState.Queued;
    public void MarkSynced(string remoteId) { RemoteId = remoteId; Sync = SyncState.Synced; SyncedAt = DateTime.Now; }
    public void MarkFailed() => Sync = SyncState.Failed;
    public void ChangeStatus(SupportStatus status) { Status = status; UpdatedAt = DateTime.Now; }
    public void SetVotes(int votes) => VoteCount = votes < 0 ? 0 : votes;
}
