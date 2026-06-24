using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.Modules.HR;

/// <summary>
/// ماژولِ منابع انسانی — حقوق‌ودستمزد + حضوروغیاب (فاز ۳، استخراجِ کامل). کدِ حقوق/حضور اکنون در این
/// اسمبلیِ مستقل است؛ هسته دیگر آن را hard-code نمی‌کند. نکته: `Employee`/`Department` (داده‌پایهٔ
/// سازمانیِ مشترک) در هسته می‌مانند چون فروش/رستوران/گردشگری هم مصرفشان می‌کنند. schema: Hrm.
/// </summary>
public sealed class HrModule : IModule
{
    public string Key => "HR";
    public string DisplayName => "منابع انسانی";
    public string Version => "1.0.0";

    /// <summary>هندلرهای MediatRِ حقوق/حضور + چک‌کنندهٔ وابستگیِ کارمند (برای حذفِ امن در هسته) ثبت می‌شوند.</summary>
    public void RegisterServices(IServiceCollection services)
    {
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(HrModule).Assembly));
        services.AddScoped<SamaHesab.Application.Common.Interfaces.IEmployeeDependencyChecker,
            Application.EmployeeDependencyChecker>();
    }

    /// <summary>مپِ EFِ موجودیت‌های حقوق/حضور (G4) — منتقل‌شده از ApplicationDbContext. schema Hrm.
    /// فیلترِ شرکت + Ignoreِ ستون‌های auditِ AuditableEntity را حلقهٔ عمومیِ DbContext پس از این اعمال می‌کند.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttendanceRecord>().ToTable("AttendanceRecords", "Hrm");
        modelBuilder.Entity<SalarySlip>().ToTable("SalarySlips", "Hrm");
        modelBuilder.Entity<PayrollSetting>().ToTable("PayrollSettings", "Hrm");
        modelBuilder.Entity<Shift>().ToTable("Shifts", "Hrm");
        modelBuilder.Entity<Holiday>().ToTable("Holidays", "Hrm");
        modelBuilder.Entity<LeaveRequest>().ToTable("LeaveRequests", "Hrm");
        modelBuilder.Entity<AttendanceDevice>().ToTable("Devices", "Hrm");
        modelBuilder.Entity<RawPunch>().ToTable("RawPunches", "Hrm");
    }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();

    public IReadOnlyList<ModulePermission> GetPermissions() => new[]
    {
        new ModulePermission("HR", "Employee",   "Manage", "مدیریتِ پرسنل (استخدام/ویرایش/غیرفعال)"),
        new ModulePermission("HR", "Payroll",    "Run",    "اجرای حقوقِ ماهانه + صدورِ فیش"),
        new ModulePermission("HR", "Payroll",    "View",   "مشاهدهٔ فیش/گزارشِ حقوق"),
        new ModulePermission("HR", "Payroll",    "Post",   "صدورِ سندِ حسابداریِ حقوق"),
        new ModulePermission("HR", "Attendance", "Manage", "ثبت/ویرایشِ تردد + شیفت/تقویم/دستگاه"),
        new ModulePermission("HR", "Attendance", "View",   "مشاهدهٔ گزارشِ حضوروغیاب"),
        new ModulePermission("HR", "Leave",      "Approve","تأیید/ردِ مرخصی"),
    };

    public IReadOnlyList<string> GetMigrationScripts() => new[]
    {
        "29_PayrollAccounts.sql",
        "39_PayrollFullSchema.sql",
        "40_PayrollSettings.sql",
        "41_AttendanceSchema.sql",
        "45_AttendanceDevices.sql",
    };
}
