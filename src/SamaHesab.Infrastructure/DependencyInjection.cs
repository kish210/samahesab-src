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
            }));

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IChequeRepository, ChequeRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IStockItemRepository, StockItemRepository>();

        // Services
        services.AddScoped<IPersianCalendarService, PersianCalendarService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExcelExportService, ExcelExportService>();
        services.AddScoped<IBackupService, BackupService>();

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
