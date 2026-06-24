using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.Contracting.Domain;

namespace SamaHesab.Modules.Contracting;

/// <summary>
/// ماژولِ پیمانکاری (فاز ۲ — اولین ماژولِ دارای لایهٔ Application/handler). schema: Con.
/// کدِ Domain + Application اکنون در این اسمبلیِ مستقل است؛ هسته فقط از طریقِ این `IModule` آن را می‌شناسد.
/// </summary>
public sealed class ContractingModule : IModule
{
    public string Key => "Contracting";
    public string DisplayName => "پیمانکاری";
    public string Version => "1.0.0";

    /// <summary>ثبتِ هندلرهای MediatRِ همین اسمبلی (کامند/کوئریِ پیمانکاری).</summary>
    public void RegisterServices(IServiceCollection services)
        => services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(ContractingModule).Assembly));

    /// <summary>مپِ EFِ موجودیت‌های پیمانکاری (G4) — منتقل‌شده از ApplicationDbContext. schema Con.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ContractProject>().ToTable("Projects", "Con");
        modelBuilder.Entity<ContractingSetting>().ToTable("Settings", "Con");
        modelBuilder.Entity<AdvancePayment>().ToTable("AdvancePayments", "Con");
        modelBuilder.Entity<Guarantee>().ToTable("Guarantees", "Con");
        modelBuilder.Entity<ProgressStatement>(b =>
        {
            b.ToTable("Statements", "Con");
            b.HasMany(s => s.Deductions).WithOne().HasForeignKey(d => d.StatementId);
        });
        modelBuilder.Entity<StatementDeduction>().ToTable("StatementDeductions", "Con");
    }

    public IReadOnlyList<ModuleMenu> GetMenus() => new[]
    {
        new ModuleMenu("Contracting", "صورت‌وضعیتِ پیمان", "ContractingStatement", Order: 10),
        new ModuleMenu("Contracting", "داشبوردِ پیمان", "ContractingDashboard", Order: 20),
        new ModuleMenu("Contracting", "گزارش‌های پیمانکاری", "ContractingReports", Order: 30),
    };

    public IReadOnlyList<ModulePermission> GetPermissions() => System.Array.Empty<ModulePermission>();
    public IReadOnlyList<string> GetMigrationScripts() => new[] { "43_Contracting.sql", "44_DemoData_Contracting.sql" };
}
