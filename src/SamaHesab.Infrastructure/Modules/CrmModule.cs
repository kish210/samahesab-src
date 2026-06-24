using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.Infrastructure.Modules;

/// <summary>
/// ماژولِ باشگاهِ مشتریان/CRM (فاز ۳ ماژولارسازی — لِینِ pc): امتیاز/وفاداری.
/// نکته: «اشخاص/طرف‌حساب» هسته است (فروش/خرید لازمش دارند)؛ فقط «باشگاه/امتیاز» ماژول است.
/// جدولِ Crm.LoyaltyTransactions فعلاً در schemaی پایه (02_CreateTables) است؛ استخراجِ
/// migrationِ جداگانه و انتقالِ نگاشتِ EF از ApplicationDbContext = پیگیریِ مشترکِ G4.
/// </summary>
public sealed class CrmModule : IModule
{
    public string Key => "CRM";
    public string DisplayName => "باشگاه مشتریان";
    public string Version => "1.0.0";

    public void RegisterServices(IServiceCollection services) { }

    /// <summary>G4: نگاشتِ Crm.LoyaltyTransactions فعلاً در ApplicationDbContext (انتقال در طراحیِ مشترک).</summary>
    public void ConfigureModel(ModelBuilder modelBuilder) { }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();

    public IReadOnlyList<ModulePermission> GetPermissions() => new[]
    {
        new ModulePermission("CRM", "Loyalty", "View",   "مشاهدهٔ امتیاز/تراکنش‌های باشگاه"),
        new ModulePermission("CRM", "Loyalty", "Manage", "تعریفِ قواعدِ امتیاز + کسر/افزایشِ دستی"),
    };

    /// <summary>جدولِ باشگاه در schemaی پایه است؛ migrationِ اختصاصی پس از استخراجِ G4 افزوده می‌شود.</summary>
    public IReadOnlyList<string> GetMigrationScripts() => System.Array.Empty<string>();
}
