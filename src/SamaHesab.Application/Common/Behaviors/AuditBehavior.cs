using System.Reflection;
using System.Text.Json;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Common.Behaviors;

/// <summary>
/// T19/T21 — حسابرسی (و در صورتِ نیاز، کنترلِ دسترسیِ) عملیاتِ حساس — cross-cut، لِینِ امنیتِ C1.
/// تطبیق بر اساسِ نامِ نوعِ فرمان تا فایلِ فرمان‌ها (لِینِ C2/مشترک) دست نخورد.
///   • انبار (T19): enforce=true ⇒ نبودِ مجوزِ Inventory.Manage ⇒ Result.Failure + (در صورتِ موفقیت) ثبتِ لاگ.
///   • حسابداری/خزانه (T21): enforce=false ⇒ فقط لاگِ حسابرسی (بدونِ تغییرِ رفتارِ مجوزِ این جریان‌ها).
/// لاگ در <c>Sec.AuditLogs</c> ثبت می‌شود (best-effort). در هر دو pipeline (WPF مستقیم + API).
/// </summary>
public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private sealed record Rule(string Module, string Feature, string PermAction, string AuditAction, bool Enforce, string Table);

    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.Ordinal)
    {
        // ── حرکاتِ انبار (RBAC + audit) ──
        ["AdjustStockCommand"]    = new("Inventory", "Manage", "", "تعدیلِ موجودی",      Enforce: true,  Table: "Inv"),
        ["TransferStockCommand"]  = new("Inventory", "Manage", "", "انتقال بین انبار",    Enforce: true,  Table: "Inv"),
        ["PostStockCountCommand"] = new("Inventory", "Manage", "", "ثبتِ انبارگردانی",    Enforce: true,  Table: "Inv"),
        // ── حسابداری (audit-only) ──
        ["PostVoucherCommand"]    = new("Accounting", "Voucher", "Post", "قطعیِ سند",     Enforce: false, Table: "Acc"),
        ["ReverseVoucherCommand"] = new("Accounting", "Voucher", "Post", "برگشتِ سند",    Enforce: false, Table: "Acc"),
        ["CloseFiscalYearCommand"]= new("Accounting", "Setup", "Manage", "بستنِ سال مالی", Enforce: false, Table: "Acc"),
        // ── گردش‌کارِ تأیید (T22): ارسال audit-only؛ تأیید/رد نیازمندِ مجوزِ Approve (enforce) ──
        ["SubmitVoucherForApprovalCommand"] = new("Accounting", "Voucher", "Create",  "ارسالِ سند برای تأیید", Enforce: false, Table: "Acc"),
        ["ApproveVoucherCommand"]           = new("Accounting", "Voucher", "Approve", "تأییدِ سند",            Enforce: true,  Table: "Acc"),
        ["RejectVoucherCommand"]            = new("Accounting", "Voucher", "Approve", "ردِّ سند",              Enforce: true,  Table: "Acc"),
        ["ReopenVoucherApprovalCommand"]    = new("Accounting", "Voucher", "Create",  "بازگشاییِ سند",         Enforce: false, Table: "Acc"),
        // ── خزانه (audit-only) ──
        ["CreatePaymentCommand"]  = new("Treasury", "Manage", "", "پرداختِ خزانه",        Enforce: false, Table: "Trs"),
        ["CreateReceiptCommand"]  = new("Treasury", "Manage", "", "دریافتِ خزانه",        Enforce: false, Table: "Trs"),
        ["CreateInterBranchTransferCommand"] = new("Treasury", "Manage", "", "تسویهٔ بین‌شعبه", Enforce: false, Table: "Trs"),
        ["PostSalaryVoucherCommand"] = new("Accounting", "Voucher", "Create", "صدورِ سندِ حقوق", Enforce: false, Table: "Hrm"),
    };

    private readonly ICurrentUserService _user;
    private readonly IRepository<AuditLog> _audit;
    private readonly IUnitOfWork _uow;

    public AuditBehavior(ICurrentUserService user, IRepository<AuditLog> audit, IUnitOfWork uow)
    { _user = user; _audit = audit; _uow = uow; }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!Rules.TryGetValue(request.GetType().Name, out var rule))
            return await next();

        if (rule.Enforce && !_user.HasPermission(rule.Module, rule.Feature, rule.PermAction))
            return Deny($"شما مجوزِ لازم برای «{rule.AuditAction}» را ندارید. با مدیر سیستم هماهنگ کنید.");

        var response = await next();

        if (response is Result r && r.Succeeded)
        {
            try
            {
                string? payload = null;
                try { payload = JsonSerializer.Serialize((object)request); } catch { /* ignore */ }
                await _audit.AddAsync(AuditLog.Create(
                    rule.AuditAction, _user.UserId, _user.Username, tableName: rule.Table, recordId: null, newValues: payload), ct);
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
        throw new UnauthorizedAccessException(message);
    }
}
