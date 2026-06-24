using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.Tourism.Domain;

namespace SamaHesab.Modules.Tourism;

/// <summary>
/// ماژولِ گردشگری (MOD-TUR). schema: Tur. کلِ Domain + Application (ثبتِ فروش/ودیعه/پورسانت/تنظیمات/
/// گزارش/سندِ خودکار) به این اسمبلیِ مستقل منتقل شد. هسته صفر رفرنس به Tourism دارد؛ کوپلینگِ HR/داشبورد
/// با اینترفیس‌های اختیاریِ هسته (ISalesCommissionProvider/ISupplierDepositAlertProvider) decouple شد.
/// </summary>
public sealed class TourismModule : IModule
{
    public string Key => "Tourism";
    public string DisplayName => "گردشگری";
    public string Version => "1.0.0";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(TourismModule).Assembly));
        // قلاب‌های هسته را پیاده می‌کند (HR پورسانت + داشبورد ودیعه). نبودِ ماژول → این‌ها ثبت نمی‌شوند.
        services.AddScoped<ISalesCommissionProvider, TourismSalesCommissionProvider>();
        services.AddScoped<ISupplierDepositAlertProvider, TourismSupplierDepositAlertProvider>();
    }

    /// <summary>مپِ EFِ موجودیت‌های گردشگری (G4) — منتقل‌شده از ApplicationDbContext. schema Tur.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductGroup>().ToTable("ProductGroups", "Tur");
        modelBuilder.Entity<TourismProduct>().ToTable("Products", "Tur");
        modelBuilder.Entity<SupplierDeposit>().ToTable("SupplierDeposits", "Tur");
        modelBuilder.Entity<TourismSetting>().ToTable("Settings", "Tur");
        modelBuilder.Entity<CommissionRule>().ToTable("CommissionRules", "Tur");
        modelBuilder.Entity<SalesCommissionEntry>().ToTable("CommissionEntries", "Tur");
        modelBuilder.Entity<SupplierDailyReport>().ToTable("SupplierDailyReports", "Tur");
        modelBuilder.Entity<TourismSale>(b =>
        {
            b.ToTable("Sales", "Tur");
            b.HasMany(s => s.Lines).WithOne().HasForeignKey(l => l.SaleId);
        });
        modelBuilder.Entity<TourismSaleLine>(b =>
        {
            b.ToTable("SaleLines", "Tur");
            b.HasMany(l => l.Passengers).WithOne().HasForeignKey(p => p.SaleLineId);
        });
        modelBuilder.Entity<SalePassenger>().ToTable("SalePassengers", "Tur");
    }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();   // صفحاتش در منوی گردشگریِ هاست
    public IReadOnlyList<ModulePermission> GetPermissions() => System.Array.Empty<ModulePermission>();
    public IReadOnlyList<string> GetMigrationScripts() => new[] { "42_Tourism.sql" };
}
