using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.Modules.Restaurant;

/// <summary>
/// ماژولِ رستوران (MOD-REST، در حالِ استخراج). schema: Rst.
///
/// وضعیت: اسکلتِ ماژول ساخته شد و در solution/میزبان ثبت می‌شود. انتقالِ کد به‌صورتِ گام‌به‌گامِ
/// سبز انجام می‌شود (به‌خاطرِ کوپلینگِ بیشتر نسبت به Contracting):
///   ۱) موجودیت‌ها (Domain/Entities/Restaurant) + Application/Restaurant → این اسمبلی.
///   ۲) `IRestaurantOrderRepository` (از فایلِ مشترکِ IRestaurantRepositories جدا) + impl (از Infrastructure) → این‌جا.
///   ۳) `RestaurantSeeder` (Infrastructure) → این‌جا.
///   ۴) `GetRestaurantDashboardQuery` (از Application/BI/MoreRoleDashboardsQuery) → این‌جا (کوئریِ ماژول).
///   ۵) حذفِ DbSet/مپِ Rst از ApplicationDbContext؛ مپ از ConfigureModelِ این ماژول.
///   ۶) رفرنسِ کنترلرِ API + VMهای WPF به این اسمبلی؛ ثبت در حلقهٔ ماژولِ میزبان.
/// تا تکمیلِ گام‌ها، مپ همچنان در هسته است و این ماژول no-op می‌ماند (build سبز).
/// </summary>
public sealed class RestaurantModule : IModule
{
    public string Key => "Restaurant";
    public string DisplayName => "رستوران";
    public string Version => "1.0.0";

    public void RegisterServices(IServiceCollection services) { /* گام ۲/۴: repo + MediatRِ ماژول */ }

    public void ConfigureModel(ModelBuilder modelBuilder) { /* گام ۵: مپِ schema Rst این‌جا منتقل می‌شود */ }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();
    public IReadOnlyList<ModulePermission> GetPermissions() => System.Array.Empty<ModulePermission>();
    public IReadOnlyList<string> GetMigrationScripts() => new[] { "09_Restaurant.sql" };
}
