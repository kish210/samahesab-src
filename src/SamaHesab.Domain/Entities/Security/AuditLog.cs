namespace SamaHesab.Domain.Entities.Security;

/// <summary>Immutable audit trail entry (maps to Sec.AuditLogs, BIGINT key).</summary>
public class AuditLog
{
    public long Id { get; private set; }
    public int? UserId { get; private set; }
    public string? Username { get; private set; }
    public string Action { get; private set; } = default!;
    public string? TableName { get; private set; }
    public string? RecordId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    private AuditLog() { }

    public static AuditLog Create(string action, int? userId, string? username,
        string? tableName = null, string? recordId = null, string? newValues = null,
        string? ipAddress = null, string? userAgent = null)
        => new()
        {
            Action = action,
            UserId = userId,
            Username = username,
            TableName = tableName,
            RecordId = recordId,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };
}
