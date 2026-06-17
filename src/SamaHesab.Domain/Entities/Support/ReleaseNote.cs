using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Support;

/// <summary>🆘 HC-2 — یادداشتِ نسخه (کَش‌شده از وردپرس): تازه‌ها/رفعِ باگ/مشکلاتِ شناخته‌شده.</summary>
public class ReleaseNote : AuditableEntity
{
    public string RemoteId { get; private set; } = default!;
    public string Version { get; private set; } = default!;
    public string? Highlights { get; private set; }   // قابلیت‌های نو (متن/Markdown)
    public string? BugFixes { get; private set; }
    public string? KnownIssues { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public bool IsCurrent { get; private set; }

    private ReleaseNote() { }

    public static ReleaseNote Create(int companyId, string remoteId, string version,
        string? highlights, string? bugFixes, string? knownIssues, DateTime? publishedAt, bool isCurrent)
        => new()
        {
            CompanyId = companyId,
            RemoteId = remoteId,
            Version = version.Trim(),
            Highlights = highlights,
            BugFixes = bugFixes,
            KnownIssues = knownIssues,
            PublishedAt = publishedAt,
            IsCurrent = isCurrent,
        };

    public void Refresh(string version, string? highlights, string? bugFixes, string? knownIssues, DateTime? publishedAt, bool isCurrent)
    {
        Version = version.Trim();
        Highlights = highlights;
        BugFixes = bugFixes;
        KnownIssues = knownIssues;
        PublishedAt = publishedAt;
        IsCurrent = isCurrent;
        UpdatedAt = DateTime.Now;
    }
}
