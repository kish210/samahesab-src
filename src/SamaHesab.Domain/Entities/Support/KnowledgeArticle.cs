using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Support;

/// <summary>
/// 🆘 HC-2 — مقالهٔ دانشنامه (کَش‌شده از وردپرس). فقط خواندنی در ERP؛ منبعِ حقیقت وردپرس است.
/// </summary>
public class KnowledgeArticle : AuditableEntity
{
    public string RemoteId { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public SupportCategory Category { get; private set; }
    public string? Summary { get; private set; }
    public string? Body { get; private set; }
    public string? Url { get; private set; }
    public string Kind { get; private set; } = "article";   // article | faq | guide | video
    public DateTime? PublishedAt { get; private set; }

    private KnowledgeArticle() { }

    public static KnowledgeArticle Create(int companyId, string remoteId, string title,
        SupportCategory category, string? summary, string? body, string? url, string kind, DateTime? publishedAt)
        => new()
        {
            CompanyId = companyId,
            RemoteId = remoteId,
            Title = title.Trim(),
            Category = category,
            Summary = summary,
            Body = body,
            Url = url,
            Kind = string.IsNullOrWhiteSpace(kind) ? "article" : kind.Trim().ToLowerInvariant(),
            PublishedAt = publishedAt,
        };

    /// <summary>به‌روزرسانیِ کَش از وردپرس.</summary>
    public void Refresh(string title, SupportCategory category, string? summary, string? body, string? url, string kind, DateTime? publishedAt)
    {
        Title = title.Trim();
        Category = category;
        Summary = summary;
        Body = body;
        Url = url;
        Kind = string.IsNullOrWhiteSpace(kind) ? Kind : kind.Trim().ToLowerInvariant();
        PublishedAt = publishedAt;
        UpdatedAt = DateTime.Now;
    }
}
