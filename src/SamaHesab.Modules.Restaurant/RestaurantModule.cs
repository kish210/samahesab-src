using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.Restaurant.Domain;
using SamaHesab.Modules.Restaurant.Infrastructure;

namespace SamaHesab.Modules.Restaurant;

/// <summary>
/// ماژولِ رستوران (MOD-REST). schema: Rst. کدِ Domain/Application/repository/seeder/داشبورد
/// به این اسمبلیِ مستقل منتقل شد؛ هسته صفر رفرنس به رستوران دارد و فقط از طریقِ این IModule می‌شناسدش.
/// </summary>
public sealed class RestaurantModule : IModule
{
    public string Key => "Restaurant";
    public string DisplayName => "رستوران";
    public string Version => "1.0.0";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<IRestaurantOrderRepository, RestaurantOrderRepository>();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(RestaurantModule).Assembly));
    }

    /// <summary>مپِ EFِ موجودیت‌های رستوران (G4) — منتقل‌شده از ApplicationDbContext. schema Rst.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hall>(b =>
        {
            b.ToTable("Halls", "Rst");
            b.Ignore(h => h.Tables);   // tables are queried directly by HallId
        });
        modelBuilder.Entity<DiningTable>().ToTable("DiningTables", "Rst");
        modelBuilder.Entity<KitchenTicket>().ToTable("KitchenTickets", "Rst");
        modelBuilder.Entity<RestaurantOrder>(b =>
        {
            b.ToTable("RestaurantOrders", "Rst");
            b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
            foreach (var p in new[] { "SubTotal", "Discount", "ServiceCharge", "Tax", "Tip", "GrandTotal", "PaidAmount" })
                b.Property(p).HasPrecision(18, 2);
        });
        modelBuilder.Entity<RestaurantOrderItem>(b =>
        {
            b.ToTable("RestaurantOrderItems", "Rst");
            b.Property(i => i.Quantity).HasPrecision(18, 3);
            foreach (var p in new[] { "UnitPrice", "DiscountAmount", "LineTotal" })
                b.Property(p).HasPrecision(18, 2);
        });
    }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();   // POS/میز/آشپزخانه لانچرِ مستقل دارند
    public IReadOnlyList<ModulePermission> GetPermissions() => System.Array.Empty<ModulePermission>();
    public IReadOnlyList<string> GetMigrationScripts() => new[] { "09_Restaurant.sql" };
}
