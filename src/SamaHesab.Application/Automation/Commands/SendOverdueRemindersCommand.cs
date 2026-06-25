using MediatR;
using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.Application.Automation.Commands;

/// <summary>
/// P2 — ارسالِ پیامکیِ یادآورهای ازپیش‌ساختهٔ بدهیِ معوق/چکِ سررسید (خروجیِ <see cref="OverdueReminderBuilder"/>).
/// ارسال اقدامِ صریحِ کاربر است؛ صداکننده (UI) باید پیش از این، خلاصه را نمایش و تأیید بگیرد.
/// ISmsService در scopeِ مدیاتور resolve می‌شود (نه در ViewModelِ ریشه).
/// </summary>
public record SendOverdueRemindersCommand(IReadOnlyList<Reminder> Reminders)
    : IRequest<SendRemindersResult>;

/// <summary>نتیجهٔ ارسال: تعدادِ موفق/ناموفق از کلِ یادآورها.</summary>
public record SendRemindersResult(int Sent, int Failed)
{
    public int Total => Sent + Failed;
}

public class SendOverdueRemindersCommandHandler
    : IRequestHandler<SendOverdueRemindersCommand, SendRemindersResult>
{
    private readonly ISmsService _sms;

    public SendOverdueRemindersCommandHandler(ISmsService sms) => _sms = sms;

    public async Task<SendRemindersResult> Handle(SendOverdueRemindersCommand req, CancellationToken ct)
    {
        int sent = 0, failed = 0;
        foreach (var r in req.Reminders)
        {
            if (string.IsNullOrWhiteSpace(r.Mobile)) { failed++; continue; }
            var ok = await _sms.SendAsync(r.Mobile, r.Message, ct);
            if (ok) sent++; else failed++;
        }
        return new SendRemindersResult(sent, failed);
    }
}
