using System.Globalization;

namespace SamaHesab.Application.Automation;

/// <summary>نوعِ یادآور.</summary>
public enum ReminderKind { OverdueDebt = 0, ChequeDueSoon = 1 }

/// <summary>ورودیِ یک یادآور (طرف‌حساب + موبایل + مبلغ + نوع + تاریخِ سررسید).</summary>
public record ReminderInput(string PartyName, string? Mobile, decimal Amount, ReminderKind Kind, string? DueDate = null);

/// <summary>یک پیامکِ یادآورِ آماده برای ارسال.</summary>
public record Reminder(string Mobile, string PartyName, ReminderKind Kind, decimal Amount, string Message);

/// <summary>
/// سازندهٔ پیامکِ یادآورِ بدهی/چک — منطقِ خالص و تست‌پذیر (P2). از دادهٔ ماندهٔ سنی‌شده/تقویمِ چک،
/// پیام‌های فارسیِ آمادهٔ ارسال (به ISmsService) می‌سازد. ردیفِ بدونِ موبایل یا با مبلغِ غیرمثبت حذف می‌شود
/// (یادآوری معنا ندارد). ارسالِ واقعی کارِ صداکننده است (اقدامِ صریحِ کاربر، نه خودکار).
/// </summary>
public static class OverdueReminderBuilder
{
    public static IReadOnlyList<Reminder> Build(IEnumerable<ReminderInput> inputs, string companyName)
    {
        var list = new List<Reminder>();
        foreach (var r in inputs)
        {
            if (string.IsNullOrWhiteSpace(r.Mobile) || r.Amount <= 0) continue;
            list.Add(new Reminder(r.Mobile!.Trim(), r.PartyName, r.Kind, r.Amount, Compose(r, companyName)));
        }
        return list;
    }

    private static string Compose(ReminderInput r, string companyName)
    {
        var amount = r.Amount.ToString("#,0", CultureInfo.InvariantCulture);
        var name = string.IsNullOrWhiteSpace(r.PartyName) ? "مشتریِ گرامی" : $"{r.PartyName}ِ گرامی";
        return r.Kind switch
        {
            ReminderKind.ChequeDueSoon =>
                $"{name}، چکِ {amount} ریالیِ شما"
                + (string.IsNullOrWhiteSpace(r.DueDate) ? "" : $" در تاریخِ {r.DueDate}")
                + $" نزدِ {companyName} سررسید می‌شود. لطفاً نسبت به تأمینِ وجه اقدام فرمایید.",
            _ =>
                $"{name}، ماندهٔ بدهیِ {amount} ریالیِ شما نزدِ {companyName} سررسیدگذشته است."
                + " لطفاً در اولین فرصت تسویه فرمایید. با تشکر.",
        };
    }
}
