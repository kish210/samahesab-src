using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.POS.Domain;

namespace SamaHesab.Modules.POS;

/// <summary>
/// ماژولِ صندوقِ فروش (POS). schema: Pos. موجودیت‌های شیفتِ صندوق + فاکتورِ معلق و فرمان‌هایشان
/// به این اسمبلیِ مستقل منتقل شد؛ هسته صفر رفرنس به POS دارد و فقط از طریقِ این IModule می‌شناسدش.
/// (فروشِ POS از مسیرِ فروشِ هسته انجام می‌شود؛ این ماژول شیفت/تعلیقِ مخصوصِ صندوق را دارد.)
/// </summary>
public sealed class PosModule : IModule
{
    public string Key => "POS";
    public string DisplayName => "صندوق فروش (POS)";
    public string Version => "1.0.0";

    public void RegisterServices(IServiceCollection services)
        => services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(PosModule).Assembly));

    /// <summary>مپِ EFِ موجودیت‌های POS (G4) — منتقل‌شده از ApplicationDbContext. schema Pos.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CashShift>(b =>
        {
            b.ToTable("CashShifts", "Pos");
            foreach (var p in new[] { "OpeningFloat", "CashSales", "CardSales", "CountedCash", "ExpectedCash", "Variance" })
                b.Property(p).HasPrecision(18, 2);
        });
        modelBuilder.Entity<HeldSale>(b =>
        {
            b.ToTable("HeldSales", "Pos");
            b.Property(h => h.Total).HasPrecision(18, 2);
        });
    }

    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();   // pos.exe لانچرِ مستقل دارد
    public IReadOnlyList<ModulePermission> GetPermissions() => System.Array.Empty<ModulePermission>();
    public IReadOnlyList<string> GetMigrationScripts() => new[] { "16_CashShifts.sql", "17_HeldSales.sql" };
}
