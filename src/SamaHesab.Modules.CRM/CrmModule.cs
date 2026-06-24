using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.CRM.Domain;

namespace SamaHesab.Modules.CRM;

/// <summary>
/// ماژولِ باشگاهِ مشتریان/CRM (فاز ۳ — استخراجِ کامل). کدِ باشگاه/امتیاز اکنون در این اسمبلیِ
/// مستقل است؛ هسته دیگر آن را hard-code نمی‌کند. نکته: «اشخاص/طرف‌حساب» هسته می‌ماند (فروش/خرید
/// لازمش دارند)؛ فقط «باشگاه/امتیاز» ماژول است. schema: Crm (جدولِ LoyaltyTransactions).
/// </summary>
public sealed class CrmModule : IModule
{
    public string Key => "CRM";
    public string DisplayName => "باشگاه مشتریان";
    public string Version => "1.0.0";

    /// <summary>هندلرهای MediatRِ باشگاه (Award/Redeem/GetCustomerLoyalty) از همین اسمبلی ثبت می‌شوند.</summary>
    public void RegisterServices(IServiceCollection services)
        => services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(CrmModule).Assembly));

    /// <summary>مپِ EFِ موجودیتِ باشگاه (G4) — منتقل‌شده از ApplicationDbContext.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
        => modelBuilder.Entity<LoyaltyTransaction>().ToTable("LoyaltyTransactions", "Crm");

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();

    public IReadOnlyList<ModulePermission> GetPermissions() => new[]
    {
        new ModulePermission("CRM", "Loyalty", "View",   "مشاهدهٔ امتیاز/تراکنش‌های باشگاه"),
        new ModulePermission("CRM", "Loyalty", "Manage", "تعریفِ قواعدِ امتیاز + کسر/افزایشِ دستی"),
    };

    /// <summary>جدولِ باشگاه در schemaی پایه (02_CreateTables) است؛ migrationِ اختصاصی پس از جداسازیِ کامل افزوده می‌شود.</summary>
    public IReadOnlyList<string> GetMigrationScripts() => System.Array.Empty<string>();
}
