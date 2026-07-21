using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Infrastructure.Repositories;
using SamaHesab.Infrastructure.Services.Backup;
using SamaHesab.Infrastructure.Services.Persian;
using SamaHesab.Infrastructure.Services.Reporting;
using SamaHesab.Infrastructure.Services.Sms;

namespace SamaHesab.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                // NOTE: do NOT enable a retrying execution strategy here — it is
                // incompatible with the user-initiated transactions used in the
                // command handlers (sales/purchase) and causes them to throw.
                sqlOptions.CommandTimeout(15);
            })
            // ماژولارسازی: کلیدِ کشِ مدل به ماژول‌های فعال وابسته شود تا نصب/حذفِ ماژول مدل را بازبسازد.
            .ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, Data.ModuleAwareModelCacheKeyFactory>());

        // ماژول‌های لِینِ pc (HR/CRM/Attendance) استخراجِ کامل شدند → در foreachِ هاست‌ها ثبت می‌شوند.
        // پلِ پورسانت→حقوق توسطِ TourismModule (ISalesCommissionProvider، کارِ laptop) ثبت می‌شود.

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IChequeRepository, ChequeRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IStockItemRepository, StockItemRepository>();
        // IRestaurantOrderRepository → ثبتش به RestaurantModule.RegisterServices منتقل شد (MOD-REST).
        services.AddScoped<IVoucherTemplateRepository, VoucherTemplateRepository>();
        services.AddScoped<IRecurringVoucherRepository, RecurringVoucherRepository>();
        services.AddScoped<IUserItemRefRepository, UserItemRefRepository>();
        services.AddScoped<IStockCountRepository, StockCountRepository>();

        // Services
        services.AddScoped<IPersianCalendarService, PersianCalendarService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IExcelImportService, ExcelImportService>();   // فاز ۱۲ G4 — ورودِ اکسل
        services.AddScoped<IUnitLookup, Services.Inventory.UnitLookup>();   // فاز ۱۲ G4.2 — جست‌وجوی واحد
        services.AddSingleton<SamaHesab.Application.Licensing.IMachineFingerprintProvider,
            Services.Licensing.MachineFingerprintProvider>();   // فاز ۱۲ P-G7 — اثرِانگشتِ دستگاه
        // پیش‌فرضِ نامحدود (سرور/تست)؛ کلاینتِ دسکتاپ آن را با نسخهٔ واقعی override می‌کند.
        services.AddSingleton<SamaHesab.Application.Licensing.ILicenseContext,
            SamaHesab.Application.Licensing.UnlimitedLicenseContext>();
        // U-LIC-FREEYEAR — بنرِ اطلاع‌رسانیِ «یک‌سالِ رایگان» (نه دروازهٔ فنی؛ نگاه کن به ServerLicenseStatus.cs).
        services.AddSingleton<SamaHesab.Application.Licensing.IServerLicenseStatusProvider,
            Services.Licensing.ServerLicenseStatusProvider>();
        services.AddScoped<IPdfService, PdfService>();   // فاز ۱۱ — P2/DT-7: PDFِ بومیِ فارسی (QuestPDF)
        services.AddScoped<IBarcodeService, BarcodeService>();   // فاز ۱۱ — P2/DT-7: تصویرِ QR برای اسناد (QRCoder)
        services.AddScoped<IBackupService, BackupService>();
        services.AddSingleton<ICompanyProvisioningService>(
            _ => new Services.CompanyProvisioningService(connectionString));   // U-MULTI-COMPANY-1

        // SMS
        var smsProvider = configuration["Sms:Provider"] ?? "null";
        services.AddScoped<ISmsProvider>(sp =>
        {
            var http = new HttpClient();
            var sender = configuration["Sms:Sender"] ?? "";
            return smsProvider switch
            {
                "kavenegar" => new KavenegarProvider(http, configuration["Sms:ApiKey"] ?? "", sender),
                "farazsms" => new FarazSmsProvider(http,
                    configuration["Sms:Username"] ?? "",
                    configuration["Sms:Password"] ?? "", sender),
                "melipayamak" => new MeliPayamakProvider(http,
                    configuration["Sms:Username"] ?? "",
                    configuration["Sms:Password"] ?? "", sender),
                _ => (ISmsProvider)new NullSmsProvider()
            };
        });
        services.AddScoped<ISmsService, SmsService>();

        return services;
    }
}
