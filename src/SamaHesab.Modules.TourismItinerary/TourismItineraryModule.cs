using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.TourismItinerary.Domain;

namespace SamaHesab.Modules.TourismItinerary;

/// <summary>
/// ماژولِ «برنامه‌ریزی اقامتی گردشگری» (TourismItinerary). schema اختصاصی: <c>Tit</c>
/// (نه <c>Tur</c> — آن مالِ ماژولِ Tourism است؛ قاعدهٔ ۴: هر ماژول schemaی خودش).
/// تعریفِ محصول+سانس، الگوریتمِ هوشمندِ پیشنهادِ برنامه، و پنلِ وبِ مهمان (تأیید/ویرایش).
/// هسته صفر رفرنس به این ماژول دارد؛ ارتباط فقط از طریقِ اینترفیس‌های هسته + IModule.
/// </summary>
public sealed class TourismItineraryModule : IModule
{
    public string Key => "TourismItinerary";
    public string DisplayName => "برنامه‌ریزی اقامتی گردشگری";
    public string Version => "1.0.0";

    public void RegisterServices(IServiceCollection services)
    {
        // هندلرهای MediatR فقط از اسمبلیِ خودِ ماژول اسکن می‌شوند (قاعدهٔ ۲).
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(TourismItineraryModule).Assembly));
    }

    /// <summary>مپِ EFِ موجودیت‌های ماژول (G4) — schema Tit. فقط برای ماژولِ فعال صدا زده می‌شود.</summary>
    public void ConfigureModel(ModelBuilder b)
    {
        b.Entity<ItineraryProduct>(e =>
        {
            e.ToTable("Products", "Tit");
            e.Ignore(p => p.NetProfit);
        });
        b.Entity<ProductSession>().ToTable("ProductSessions", "Tit");
        b.Entity<GuestItinerary>(e =>
        {
            e.ToTable("GuestItineraries", "Tit");
            e.Ignore(g => g.TotalSale);
            e.Ignore(g => g.TotalProfit);
            e.HasIndex(g => g.Token).IsUnique();
            e.HasMany(g => g.Stops).WithOne().HasForeignKey(s => s.ItineraryId);
        });
        b.Entity<ItineraryStop>().ToTable("ItineraryStops", "Tit");
    }

    /// <summary>منوهای ماژول — فقط با نصب+فعال‌بودن نمایش داده می‌شوند (میزبان آن‌ها را به منوی خود ترجمه می‌کند).</summary>
    public IReadOnlyList<ModuleMenu> GetMenus() => new[]
    {
        new ModuleMenu("Tourism", "محصولاتِ اقامتی", "ItineraryProducts", "Bed", 210),
        new ModuleMenu("Tourism", "برنامه‌ریزِ اقامتی", "ItineraryPlanner", "MapMarkerPath", 211),
    };

    public IReadOnlyList<ModulePermission> GetPermissions() => new[]
    {
        new ModulePermission("TourismItinerary", "Product", "Manage", "مدیریتِ محصولاتِ اقامتی"),
        new ModulePermission("TourismItinerary", "Itinerary", "Generate", "تولیدِ برنامهٔ اقامتی"),
    };

    public IReadOnlyList<string> GetMigrationScripts() => new[] { "51_TourismItinerary.sql" };
}
