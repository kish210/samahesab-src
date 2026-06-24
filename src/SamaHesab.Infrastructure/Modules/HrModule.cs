using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.Infrastructure.Modules;

/// <summary>
/// ماژولِ منابع انسانی (فاز ۳ ماژولارسازی — لِینِ pc): حقوق‌ودستمزد + حضوروغیاب.
/// مجوزها و اسکریپت‌های مهاجرتِ اختصاصیِ خود را ثبت می‌کند. نگاشتِ EFِ موجودیت‌های Hrm
/// فعلاً در ApplicationDbContext است؛ انتقال به ConfigureModel در جلسهٔ مشترکِ G4 با laptop.
/// </summary>
public sealed class HrModule : IModule
{
    public string Key => "HR";
    public string DisplayName => "منابع انسانی";
    public string Version => "1.0.0";

    /// <summary>هندلرهای MediatRِ HR در اسمبلیِ Application اسکن می‌شوند؛ سرویسِ اختصاصیِ DI ندارد.</summary>
    public void RegisterServices(IServiceCollection services) { }

    /// <summary>G4: نگاشتِ موجودیت‌های Hrm فعلاً در ApplicationDbContext (انتقال در طراحیِ مشترک).</summary>
    public void ConfigureModel(ModelBuilder modelBuilder) { }

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
