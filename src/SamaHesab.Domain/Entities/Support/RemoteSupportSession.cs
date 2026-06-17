using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Support;

/// <summary>
/// 🆘 HC-2/HC-6 — نشستِ پشتیبانیِ ریموت. یک «کدِ پشتیبانیِ» یک‌بارمصرف تولید می‌شود که مشتری
/// به کارشناس می‌دهد؛ نشست/لاگ ذخیره می‌شود (بدونِ کنترلِ ریموتِ واقعی — فقط هماهنگی/ردگیری).
/// </summary>
public class RemoteSupportSession : AuditableEntity
{
    public string Code { get; private set; } = default!;          // کدِ پشتیبانی (مثل «SH-7F3A-21»)
    public RemoteSessionStatus Status { get; private set; } = RemoteSessionStatus.Pending;
    public string? RequestedBy { get; private set; }
    public string? Note { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public string? LogPath { get; private set; }

    private RemoteSupportSession() { }

    public static RemoteSupportSession Open(int companyId, string code, string? requestedBy, string? note, int validMinutes = 60)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کدِ پشتیبانی الزامی است.");
        return new RemoteSupportSession
        {
            CompanyId = companyId,
            Code = code.Trim(),
            RequestedBy = requestedBy,
            Note = note,
            ExpiresAt = DateTime.Now.AddMinutes(validMinutes <= 0 ? 60 : validMinutes),
        };
    }

    public void Activate()
    {
        if (Status != RemoteSessionStatus.Pending) throw new InvalidOperationException("نشست قابلِ فعال‌سازی نیست.");
        Status = RemoteSessionStatus.Active;
        StartedAt = DateTime.Now;
    }

    public void End(string? logPath = null)
    {
        Status = RemoteSessionStatus.Ended;
        EndedAt = DateTime.Now;
        if (logPath != null) LogPath = logPath;
    }

    public void Expire() => Status = RemoteSessionStatus.Expired;

    public bool IsValid => Status is RemoteSessionStatus.Pending or RemoteSessionStatus.Active && DateTime.Now <= ExpiresAt;
}
