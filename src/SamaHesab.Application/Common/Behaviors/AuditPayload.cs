using System.Text.Encodings.Web;
using System.Text.Json;

namespace SamaHesab.Application.Common.Behaviors;

/// <summary>
/// سریال‌سازیِ payloadِ لاگِ حسابرسی (BUG-9). پیش‌فرضِ System.Text.Json نویسه‌های غیر-ASCII
/// (فارسی) را به <c>\uXXXX</c> می‌گریزاند → جزئیاتِ لاگ ناخوانا می‌شد. با UnsafeRelaxedJsonEscaping
/// متنِ فارسی خوانا ذخیره می‌شود (خروجی به DB می‌رود، نه HTML؛ پس ریسکِ XSS موضوعیت ندارد).
/// </summary>
public static class AuditPayload
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
}
