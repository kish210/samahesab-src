using System.Reflection;
using System.Text.Json;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Common.Behaviors;

/// <summary>
/// T19 — حسابرسی و کنترلِ دسترسیِ «حرکاتِ انبار» (cross-cut، لِینِ امنیتِ C1).
/// برای فرمان‌های تعدیل/انتقال/انبارگردانی (تطبیق بر اساسِ نامِ نوع تا فایلِ فرمان‌ها دست نخورد):
///   • RBAC: نیاز به مجوزِ <c>Inventory.Manage</c> — نبودِ مجوز ⇒ Result.Failure (نه استثنا).
///   • Audit: پس از موفقیت، یک ردیف در <c>Sec.AuditLogs</c> ثبت می‌شود (best-effort).
/// در هر دو مسیر اجرا می‌شود (کلاینتِ مستقیم و API)، چون در هر دو pipeline ثبت شده است.
/// </summary>
public class InventoryAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // نامِ نوعِ فرمان → عملِ حسابرسی. (Start/SetLine حرکتِ واقعی نیستند؛ فقط Post.)
    private static readonly Dictionary<string, string> Audited = new(StringComparer.Ordinal)
    {
        ["AdjustStockCommand"]    = "تعدیلِ موجودی",
        ["TransferStockCommand"]  = "انتقال بین انبار",
        ["PostStockCountCommand"] = "ثبتِ انبارگردانی",
    };

    private readonly ICurrentUserService _user;
    private readonly IRepository<AuditLog> _audit;
    private readonly IUnitOfWork _uow;

    public InventoryAuditBehavior(ICurrentUserService user, IRepository<AuditLog> audit, IUnitOfWork uow)
    { _user = user; _audit = audit; _uow = uow; }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!Audited.TryGetValue(request.GetType().Name, out var action))
            return await next();

        // ── RBAC ──
        if (!_user.HasPermission("Inventory", "Manage", ""))
            return Deny("شما مجوزِ «عملیاتِ انبار» را ندارید. با مدیر سیستم هماهنگ کنید.");

        var response = await next();

        // ── Audit (فقط در صورتِ موفقیت؛ شکستِ ثبتِ لاگ نباید عملیات را خراب کند) ──
        if (response is Result r && r.Succeeded)
        {
            try
            {
                string? payload = null;
                try { payload = JsonSerializer.Serialize((object)request); } catch { /* ignore */ }
                await _audit.AddAsync(AuditLog.Create(
                    action, _user.UserId, _user.Username, tableName: "Inv", recordId: null, newValues: payload), ct);
                await _uow.SaveChangesAsync(ct);
            }
            catch { /* حسابرسی best-effort است */ }
        }
        return response;
    }

    /// <summary>یک Result/Result&lt;T&gt;ِ ناموفق می‌سازد (بدونِ استثنا، تا UI پیام را تمیز نشان دهد).</summary>
    private static TResponse Deny(string message)
    {
        var m = typeof(TResponse).GetMethod("Failure",
            BindingFlags.Public | BindingFlags.Static, binder: null, types: new[] { typeof(string[]) }, modifiers: null);
        if (m is not null)
            return (TResponse)m.Invoke(null, new object[] { new[] { message } })!;
        // اگر TResponse از نوعِ Result نباشد (نباید برای این فرمان‌ها رخ دهد) → استثنا.
        throw new UnauthorizedAccessException(message);
    }
}
