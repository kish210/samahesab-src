using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.Modules.HR;

/// <summary>
/// ماژولِ حقوق‌ودستمزد (فاز ۳، استخراجِ کامل). کدِ حقوق در این اسمبلیِ مستقل است؛ هسته آن را
/// hard-code نمی‌کند. حضوروغیاب ماژولِ جداگانه (Modules.Attendance) است و حقوق از طریقِ اینترفیسِ
/// `IAttendanceAggregateProvider` (هسته) کارکردِ ماه را می‌گیرد — بی‌وابستگیِ مستقیم. نکته:
/// `Employee`/`Department` (داده‌پایهٔ مشترک) در هسته می‌مانند. schema: Hrm.
/// </summary>
public sealed class HrModule : IModule
{
    public string Key => "HR";
    public string DisplayName => "حقوق و دستمزد";
    public string Version => "1.0.0";

    /// <summary>هندلرهای MediatRِ حقوق + چک‌کنندهٔ وابستگیِ کارمند (سابقهٔ فیش) برای حذفِ امن در هسته.</summary>
    public void RegisterServices(IServiceCollection services)
    {
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(HrModule).Assembly));
        services.AddScoped<SamaHesab.Application.Common.Interfaces.IEmployeeDependencyChecker,
            Application.PayrollEmployeeDependencyChecker>();
    }

    /// <summary>مپِ EFِ موجودیت‌های حقوق (G4). فیلترِ شرکت + Ignoreِ ستون‌های audit را حلقهٔ عمومیِ DbContext اعمال می‌کند.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SalarySlip>().ToTable("SalarySlips", "Hrm");
        modelBuilder.Entity<PayrollSetting>().ToTable("PayrollSettings", "Hrm");
    }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();

    public IReadOnlyList<ModulePermission> GetPermissions() => new[]
    {
        new ModulePermission("HR", "Employee", "Manage", "مدیریتِ پرسنل (استخدام/ویرایش/غیرفعال)"),
        new ModulePermission("HR", "Payroll",  "Run",    "اجرای حقوقِ ماهانه + صدورِ فیش"),
        new ModulePermission("HR", "Payroll",  "View",   "مشاهدهٔ فیش/گزارشِ حقوق"),
        new ModulePermission("HR", "Payroll",  "Post",   "صدورِ سندِ حسابداریِ حقوق"),
    };

    public IReadOnlyList<string> GetMigrationScripts() => new[]
    {
        "29_PayrollAccounts.sql",
        "39_PayrollFullSchema.sql",
        "40_PayrollSettings.sql",
    };
}
