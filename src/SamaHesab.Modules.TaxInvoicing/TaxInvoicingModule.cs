using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing;

/// <summary>
/// ماژولِ اختیاریِ «سامانهٔ مودیان و پایانه‌هایِ فروشگاهی» — ارسالِ الکترونیکیِ فاکتورِ فروش به سازمانِ
/// امورِ مالیاتیِ ایران (JWS/JWE طبقِ استانداردِ tp.tax.gov.ir). schema: Tax. هسته صفر رفرنس به این
/// ماژول دارد؛ اتصال با فاکتورِ فروش فقط از طریقِ Id (بدونِ navigation/JOIN، طبقِ قاعدهٔ removability).
/// ⚠️ بدونِ اعتبارنامهٔ Sandbox/واقعی ساخته شده — رمزنگاری/صف/UI با تستِ خودکار (نه سرورِ واقعی) تأیید
/// شده‌اند؛ پذیرشِ واقعیِ سازمان هنوز تأییدنشده است (به todo.rm مراجعه شود).
/// </summary>
public sealed class TaxInvoicingModule : IModule
{
    public string Key => "TaxInvoicing";
    public string DisplayName => "صورتحسابِ الکترونیکیِ مودیان";
    public string Version => "1.0.0";

    public void RegisterServices(IServiceCollection services)
    {
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(TaxInvoicingModule).Assembly));
        services.AddSingleton<Crypto.IModianCryptoService, Crypto.ModianCryptoService>();
        services.AddSingleton<Crypto.IModianCertificateProvider, Crypto.ModianCertificateProvider>();
        services.AddHttpClient<Application.IModianApiClient, Application.ModianApiClient>();
    }

    /// <summary>مپِ EFِ موجودیت‌هایِ مودیان (G4). schema Tax.</summary>
    public void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ElectronicInvoiceSubmission>(e =>
        {
            e.ToTable("Submissions", "Tax");
            e.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        });
        modelBuilder.Entity<TaxItemCode>(e =>
        {
            e.ToTable("ItemCodes", "Tax");
            e.HasIndex(c => new { c.CompanyId, c.ProductId }).IsUnique();
        });
        modelBuilder.Entity<ModianSettings>(e =>
        {
            e.ToTable("Settings", "Tax");
            e.HasIndex(s => s.CompanyId).IsUnique();
        });
    }

    // UIِ صفحاتِ تنظیمات/فهرستِ ارسال هنوز ساخته نشده (فازِ بعدی) — تا آن‌موقع منو خالی می‌ماند
    // تا NavKeyِ بی‌صفحه در منویِ کاربر ظاهر نشود.
    public IReadOnlyList<ModuleMenu> GetMenus() => System.Array.Empty<ModuleMenu>();

    public IReadOnlyList<ModulePermission> GetPermissions() => new[]
    {
        new ModulePermission("TaxInvoicing", "Submissions", "View", "مشاهدهٔ صورتحساب‌هایِ الکترونیکی"),
        new ModulePermission("TaxInvoicing", "Submissions", "Send", "ارسال/تلاشِ مجددِ صورتحساب"),
        new ModulePermission("TaxInvoicing", "Settings", "Manage", "مدیریتِ تنظیماتِ مودیان"),
    };

    public IReadOnlyList<string> GetMigrationScripts() => new[] { "63_TaxInvoicing.sql" };
}
