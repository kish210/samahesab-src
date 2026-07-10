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
    private sealed record Rule(string Module, string Feature, string PermAction, string AuditAction, bool Enforce, string Table, bool Sensitive = false);

    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.Ordinal)
    {
        // ── حرکاتِ انبار (RBAC + audit) ──
        ["AdjustStockCommand"]    = new("Inventory", "Manage", "", "تعدیلِ موجودی",      Enforce: true,  Table: "Inv"),
        ["TransferStockCommand"]  = new("Inventory", "Manage", "", "انتقال بین انبار",    Enforce: true,  Table: "Inv"),
        ["PostStockCountCommand"] = new("Inventory", "Manage", "", "ثبتِ انبارگردانی",    Enforce: true,  Table: "Inv"),
        // ── حسابداری (audit-only) ──
        ["CreateVoucherCommand"]  = new("Accounting", "Voucher", "Create", "ثبتِ سندِ دستی", Enforce: false, Table: "Acc"),
        ["CreateVoucherFromTemplateCommand"] = new("Accounting", "Voucher", "Create", "ثبتِ سند از الگو", Enforce: false, Table: "Acc"),
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
        ["ChangeChequeStatusCommand"] = new("Treasury", "Manage", "", "تغییرِ وضعیتِ چک", Enforce: false, Table: "Trs"),
        ["PostSalaryVoucherCommand"] = new("Accounting", "Voucher", "Create", "صدورِ سندِ حقوق", Enforce: false, Table: "Hrm"),
        // ── امنیت (audit-only؛ Sensitive=true ⇒ payload لاگ نمی‌شود تا رمز در Sec.AuditLogs نشت نکند) ──
        ["ChangeUserPasswordCommand"] = new("Security", "User", "Manage", "تغییرِ رمزِ کاربر", Enforce: false, Table: "Sec", Sensitive: true),
        // ── CR-C1 (X1): تکمیلِ پوششِ حسابرسی — همگی audit-only (بدونِ تغییرِ رفتار). تطبیق با نامِ فرمان. ──
        // فروش/خرید (پولی) —
        ["CreateSalesInvoiceCommand"]   = new("Sales", "Invoice", "Create", "ثبتِ فاکتورِ فروش", Enforce: false, Table: "Sal"),
        ["PostSalesInvoiceCommand"]     = new("Sales", "Invoice", "Post", "قطعیِ فاکتورِ فروش", Enforce: false, Table: "Sal"),
        ["CreateSalesReturnCommand"]    = new("Sales", "Invoice", "Create", "برگشت از فروش", Enforce: false, Table: "Sal"),
        ["CreatePurchaseInvoiceCommand"]= new("Purchase", "Invoice", "Create", "ثبتِ فاکتورِ خرید", Enforce: false, Table: "Pur"),
        ["CreatePurchaseReturnCommand"] = new("Purchase", "Invoice", "Create", "برگشت از خرید", Enforce: false, Table: "Pur"),
        ["CreatePurchaseOrderFromReorderCommand"] = new("Purchase", "Order", "Create", "سفارشِ خرید از نقطهٔ سفارش", Enforce: false, Table: "Pur"),
        // طرف‌حساب/کالا (master data — حذف/ویرایش طبقِ سندِ آمادگیِ تجاری) —
        ["CreateCustomerCommand"]       = new("Customers", "Party", "Create", "ثبتِ طرف‌حساب", Enforce: false, Table: "Crm"),
        ["UpdateCustomerCommand"]       = new("Customers", "Party", "Update", "ویرایشِ طرف‌حساب", Enforce: false, Table: "Crm"),
        ["DeleteCustomerAttachmentCommand"] = new("Customers", "Party", "Update", "حذفِ پیوستِ طرف‌حساب", Enforce: false, Table: "Crm"),
        ["CreateProductCommand"]        = new("Inventory", "Product", "Create", "ثبتِ کالا", Enforce: false, Table: "Inv"),
        ["UpdateProductPricesCommand"]  = new("Inventory", "Product", "Update", "تغییرِ قیمتِ کالا", Enforce: false, Table: "Inv"),
        ["SaveProductDiscountTiersCommand"] = new("Inventory", "Product", "Update", "پلکانِ تخفیفِ کالا", Enforce: false, Table: "Inv"),
        ["CreateWarehouseCommand"]      = new("Inventory", "Warehouse", "Create", "ثبتِ انبار", Enforce: false, Table: "Inv"),
        // انبار (حرکاتِ موجودی) —
        ["ReceiveStockCommand"]         = new("Inventory", "Manage", "", "رسیدِ انبار", Enforce: false, Table: "Inv"),
        ["IssueStockCommand"]           = new("Inventory", "Manage", "", "حوالهٔ انبار", Enforce: false, Table: "Inv"),
        ["ReceiveBatchCommand"]         = new("Inventory", "Manage", "", "رسیدِ بچ", Enforce: false, Table: "Inv"),
        ["IssueBatchCommand"]           = new("Inventory", "Manage", "", "حوالهٔ بچ", Enforce: false, Table: "Inv"),
        ["SaveBatchCommand"]            = new("Inventory", "Manage", "", "ثبتِ بچ", Enforce: false, Table: "Inv"),
        ["SaveSerialCommand"]           = new("Inventory", "Manage", "", "ثبتِ سریال", Enforce: false, Table: "Inv"),
        // حسابداری (دامنه‌ها/حساب) —
        ["DeleteAccountCommand"]        = new("Accounting", "Setup", "Manage", "حذفِ حساب", Enforce: false, Table: "Acc"),
        ["SaveAccountCommand"]          = new("Accounting", "Setup", "Manage", "ثبت/ویرایشِ حساب", Enforce: false, Table: "Acc"),
        ["SaveFiscalYearCommand"]       = new("Accounting", "Setup", "Manage", "ثبتِ سال مالی", Enforce: false, Table: "Acc"),
        ["SaveCostCenterCommand"]       = new("Accounting", "Setup", "Manage", "ثبتِ مرکز هزینه", Enforce: false, Table: "Acc"),
        ["SaveProjectCommand"]          = new("Accounting", "Setup", "Manage", "ثبتِ پروژه", Enforce: false, Table: "Acc"),
        ["PostOpeningBalanceCommand"]   = new("Accounting", "Voucher", "Create", "ثبتِ ماندهٔ اول دوره", Enforce: false, Table: "Acc"),
        ["SaveVoucherTemplateCommand"]  = new("Accounting", "Setup", "Manage", "ثبتِ قالبِ سند", Enforce: false, Table: "Acc"),
        ["SaveRecurringVoucherCommand"] = new("Accounting", "Setup", "Manage", "ثبتِ سندِ تکرارشونده", Enforce: false, Table: "Acc"),
        ["GenerateDueRecurringVouchersCommand"] = new("Accounting", "Voucher", "Create", "تولیدِ اسنادِ تکرارشونده", Enforce: false, Table: "Acc"),
        // امنیت (تغییرِ سطحِ دسترسی — حساس) —
        ["SaveRoleCommand"]             = new("Security", "Manage", "", "ثبت/ویرایشِ نقش", Enforce: false, Table: "Sec"),
        ["SetRolePermissionsCommand"]   = new("Security", "Manage", "", "تنظیمِ مجوزهای نقش", Enforce: false, Table: "Sec"),
        ["SetUserRolesCommand"]         = new("Security", "Manage", "", "تخصیصِ نقشِ کاربر", Enforce: false, Table: "Sec"),
        ["SaveBranchCommand"]           = new("Security", "Manage", "", "ثبتِ شعبه", Enforce: false, Table: "Cfg"),
        ["ToggleBranchCommand"]         = new("Security", "Manage", "", "فعال/غیرفعالِ شعبه", Enforce: false, Table: "Cfg"),
        // HR (حقوق/کارکنان/تردد) —
        ["SaveEmployeeCommand"]         = new("HR", "Employee", "Manage", "ثبت/ویرایشِ کارمند", Enforce: false, Table: "Hrm"),
        ["DeleteEmployeeCommand"]       = new("HR", "Employee", "Manage", "حذفِ کارمند", Enforce: false, Table: "Hrm"),
        ["RunMonthlyPayrollCommand"]    = new("HR", "Payroll", "Manage", "محاسبهٔ دسته‌ایِ حقوق", Enforce: false, Table: "Hrm"),
        ["SavePayrollSettingsCommand"]  = new("HR", "Payroll", "Manage", "تنظیماتِ حقوق", Enforce: false, Table: "Hrm"),
        ["RequestLeaveCommand"]         = new("HR", "Attendance", "Manage", "درخواستِ مرخصی", Enforce: false, Table: "Hrm"),
        ["DecideLeaveCommand"]          = new("HR", "Attendance", "Manage", "تصمیمِ مرخصی", Enforce: false, Table: "Hrm"),
        ["MarkBatchAttendanceCommand"]  = new("HR", "Attendance", "Manage", "علامتِ دسته‌ایِ تردد", Enforce: false, Table: "Hrm"),
        ["ProcessRawPunchesCommand"]    = new("HR", "Attendance", "Manage", "پردازشِ ترددِ خام", Enforce: false, Table: "Hrm"),
        ["SaveShiftCommand"]            = new("HR", "Attendance", "Manage", "ثبتِ شیفت", Enforce: false, Table: "Hrm"),
        ["DeleteShiftCommand"]          = new("HR", "Attendance", "Manage", "حذفِ شیفت", Enforce: false, Table: "Hrm"),
        ["SaveDeviceCommand"]           = new("HR", "Attendance", "Manage", "ثبتِ دستگاهِ تردد", Enforce: false, Table: "Hrm"),
        // گردشگری —
        ["CreateTourismSaleCommand"]    = new("Tourism", "Sale", "Create", "فروشِ گردشگری", Enforce: false, Table: "Tur"),
        ["TopUpSupplierDepositCommand"] = new("Tourism", "Deposit", "Manage", "شارژِ ودیعهٔ تأمین‌کننده", Enforce: false, Table: "Tur"),
        ["ReconcileSupplierDailyReportCommand"] = new("Tourism", "Report", "Manage", "آشتیِ گزارشِ روزانه", Enforce: false, Table: "Tur"),
        ["SaveTourismSettingsCommand"]  = new("Tourism", "Setup", "Manage", "تنظیماتِ گردشگری", Enforce: false, Table: "Tur"),
        // پیمانکاری —
        ["PostProgressStatementCommand"]= new("Contracting", "Statement", "Post", "قطعیِ صورت‌وضعیت", Enforce: false, Table: "Con"),
        ["ReceiveAdvanceCommand"]       = new("Contracting", "Advance", "Manage", "دریافتِ پیش‌پرداخت", Enforce: false, Table: "Con"),
        ["ReleaseDepositCommand"]       = new("Contracting", "Deposit", "Manage", "آزادسازیِ سپرده", Enforce: false, Table: "Con"),
        ["RegisterGuaranteeCommand"]    = new("Contracting", "Guarantee", "Manage", "ثبتِ ضمانت‌نامه", Enforce: false, Table: "Con"),
        ["ReleaseGuaranteeCommand"]     = new("Contracting", "Guarantee", "Manage", "آزادسازیِ ضمانت‌نامه", Enforce: false, Table: "Con"),
        ["SaveContractingSettingsCommand"] = new("Contracting", "Setup", "Manage", "تنظیماتِ پیمانکاری", Enforce: false, Table: "Con"),
        // اسناد/قالب —
        ["SaveDocumentTemplateCommand"] = new("Accounting", "Setup", "Manage", "ثبتِ قالبِ چاپ", Enforce: false, Table: "Doc"),
        ["DeleteDocumentTemplateCommand"] = new("Accounting", "Setup", "Manage", "حذفِ قالبِ چاپ", Enforce: false, Table: "Doc"),
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
                // برای فرمان‌های حساس (مثلِ تغییرِ رمز) محتوای فرمان سریال نمی‌شود تا راز در لاگ نشت نکند.
                if (!rule.Sensitive)
                    try { payload = AuditPayload.Serialize((object)request); } catch { /* ignore */ }
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
