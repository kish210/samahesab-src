using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SamaHesab.Modules.Abstractions;

/// <summary>
/// قراردادِ یک ماژولِ اختیاریِ SamaHesab. هسته فقط این اینترفیس را می‌شناسد، نه پیاده‌سازیِ ماژول‌ها.
/// هر ماژول خودش را ثبت می‌کند: سرویس‌ها (DI/MediatR)، موجودیت‌های EF، منوها، مجوزها، گزارش‌ها، ویجت‌ها.
/// </summary>
public interface IModule
{
    /// <summary>کلیدِ ماژول — مطابقِ ModuleService (مثلِ "Tourism", "POS").</summary>
    string Key { get; }

    /// <summary>نامِ نمایشیِ فارسی.</summary>
    string DisplayName { get; }

    /// <summary>نسخهٔ ماژول.</summary>
    string Version { get; }

    /// <summary>ثبتِ سرویس‌های DI و هندلرهای MediatRِ ماژول.</summary>
    void RegisterServices(IServiceCollection services);

    /// <summary>
    /// مپِ EFِ موجودیت‌های ماژول روی ApplicationDbContext (G4: تا هسته موجودیتِ ماژول را hard-code نکند).
    /// فقط برای ماژول‌های نصب‌شده/فعال صدا زده می‌شود.
    /// </summary>
    void ConfigureModel(ModelBuilder modelBuilder);

    /// <summary>منوهای ماژول (فقط با نصب+فعال‌بودن نمایش داده می‌شوند).</summary>
    IReadOnlyList<ModuleMenu> GetMenus();

    /// <summary>مجوزهای ماژول (به کاتالوگِ مجوزِ هسته افزوده می‌شوند).</summary>
    IReadOnlyList<ModulePermission> GetPermissions();

    /// <summary>اسکریپت‌های مهاجرتِ schemaی اختصاصیِ ماژول (نسبت به پوشهٔ database).</summary>
    IReadOnlyList<string> GetMigrationScripts();
}

/// <summary>یک آیتمِ منوی ماژول — مستقل از فناوریِ UI (میزبان آن را به منوی خود ترجمه می‌کند).</summary>
public sealed record ModuleMenu(
    string ParentKey,   // گروهِ منو (مثلِ "Tourism") یا "" برای ریشه
    string Title,
    string NavKey,      // کلیدِ صفحه برای ناوبری
    string Icon = "",
    int Order = 100);

/// <summary>یک مجوزِ ماژول.</summary>
public sealed record ModulePermission(
    string Module,      // مثلِ "Tourism"
    string Feature,
    string Action,
    string DisplayName);

/// <summary>توصیفِ یک گزارشِ ماژول.</summary>
public sealed record ModuleReport(
    string Key,
    string Title,
    string NavKey);
