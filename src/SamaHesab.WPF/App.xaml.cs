using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Windows;
using MediatR;
using SamaHesab.Application.Common.Behaviors;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using SamaHesab.WPF.ViewModels.Dashboard;
using SamaHesab.WPF.ViewModels.Accounting;
using SamaHesab.WPF.ViewModels.Inventory;
using SamaHesab.WPF.ViewModels.Sales;
using SamaHesab.WPF.ViewModels.Purchase;
using SamaHesab.WPF.ViewModels.POS;
using SamaHesab.WPF.ViewModels.CRM;
using SamaHesab.WPF.ViewModels.HRM;
using SamaHesab.WPF.ViewModels.Reports;
using SamaHesab.WPF.ViewModels.Settings;
using SamaHesab.WPF.Views.Shell;

namespace SamaHesab.WPF;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ─── Surface every startup/runtime error instead of silently exiting ──
        DispatcherUnhandledException += (_, ev) =>
        {
            ShowFatal(ev.Exception);
            ev.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ev) =>
            ShowFatal(ev.ExceptionObject as Exception);

        try
        {
            await StartAppAsync(e);
        }
        catch (Exception ex)
        {
            ShowFatal(ex);
        }
    }

    private async Task StartAppAsync(StartupEventArgs e)
    {
        // Ensure %AppData%\SamaHesab + a default connection string exist (writable).
        Services.AppSettingsStore.EnsureInitialized();

        // Apply the saved Telerik theme (runtime-switchable).
        Services.ThemeManager.Apply(Services.AppSettingsStore.GetTheme());

        // ─── Serilog (writable log folder) ────────────────────────────────────
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                System.IO.Path.Combine(Services.AppSettingsStore.LogDirectory, "samaHesab-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30, fileSizeLimitBytes: 10_000_000)
            .CreateLogger();

        // ─── Host ─────────────────────────────────────────────────────────────
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, cfg) =>
            {
                cfg.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                   .AddJsonFile("appsettings.json", optional: true)
                   .AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true)
                   // User-editable file (overrides appsettings) – set from the login Settings dialog.
                   .AddJsonFile(Services.AppSettingsStore.FilePath, optional: true, reloadOnChange: true)
                   .AddEnvironmentVariables();
            })
            .ConfigureServices((ctx, services) =>
            {
                // Infrastructure
                services.AddInfrastructure(ctx.Configuration);

                // MediatR + Pipelines
                services.AddMediatR(cfg => {
                    cfg.RegisterServicesFromAssembly(typeof(Application.Accounting.Commands.CreateVoucherCommand).Assembly);
                    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
                    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
                });

                // WPF Services
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<ICurrentUserService, CurrentUserService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<VoucherListViewModel>();
                services.AddTransient<VoucherEditViewModel>();
                services.AddTransient<ChartOfAccountsViewModel>();
                services.AddTransient<ChequeListViewModel>();
                services.AddTransient<BankAccountViewModel>();
                services.AddTransient<ProductListViewModel>();
                services.AddTransient<ProductEditViewModel>();
                services.AddTransient<WarehouseViewModel>();
                services.AddTransient<StockAdjustViewModel>();
                services.AddTransient<SalesInvoiceListViewModel>();
                services.AddTransient<SalesInvoiceEditViewModel>();
                services.AddTransient<PurchaseInvoiceEditViewModel>();
                services.AddTransient<PosViewModel>();
                services.AddTransient<CustomerListViewModel>();
                services.AddTransient<CustomerEditViewModel>();
                services.AddTransient<SupplierListViewModel>();
                services.AddTransient<EmployeeListViewModel>();
                services.AddTransient<EmployeeEditViewModel>();
                services.AddTransient<SalaryViewModel>();
                services.AddTransient<AttendanceViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<CompanySettingsViewModel>();
                services.AddTransient<BackupViewModel>();
                services.AddTransient<UserManagementViewModel>();

                // Windows
                services.AddTransient<LoginWindow>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        // ─── Database connectivity check (NON-blocking) ───────────────────────
        // Login does not need the DB, so never block the UI on it. Just probe in
        // the background and log; the schema is created from the SQL scripts.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _host.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var ok = await db.Database.CanConnectAsync();
                Log.Information("اتصال پایگاه داده: {Ok}", ok ? "برقرار" : "ناموفق");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "اتصال اولیه به پایگاه داده برقرار نشد");
            }
        });

        // ─── Capture screenshots of every screen (dev only) ───────────────────
        if (Environment.GetEnvironmentVariable("SAMA_SHOTS") == "1")
        {
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "مدیر سیستم", new[] { "ADMIN" }, Array.Empty<string>());
            var w = _host.Services.GetRequiredService<MainWindow>();
            w.Show();
            await RunScreenshotsAsync(w);
            Shutdown();
            return;
        }

        // ─── Self-test of the real persistence paths (dev only) ────────────────
        if (Environment.GetEnvironmentVariable("SAMA_SELFTEST") == "1")
        {
            await RunSelfTestAsync();
            Shutdown();
            return;
        }

        // ─── Show Login (or skip straight to the shell for UI smoke-tests) ─────
        if (Environment.GetEnvironmentVariable("SAMA_SKIP_LOGIN") == "1")
        {
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "مدیر سیستم",
                    new[] { "ADMIN" }, Array.Empty<string>());
            _host.Services.GetRequiredService<MainWindow>().Show();
            return;
        }

        var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    private async Task RunScreenshotsAsync(Window w)
    {
        var dir = @"D:\duc\sama-hesab\screenshot";
        System.IO.Directory.CreateDirectory(dir);
        var vm = (ViewModels.Shell.MainViewModel)w.DataContext;

        var pages = new (string Key, string File)[]
        {
            ("Dashboard","01_داشبورد"), ("Vouchers","02_اسناد_حسابداری"),
            ("VoucherEdit","03_ثبت_سند"), ("ChartOfAccounts","04_نمودار_حسابها"),
            ("Cheques","05_چکها"), ("BankAccounts","06_حسابهای_بانکی"),
            ("Products","07_کالاها"), ("ProductEdit","08_ویرایش_کالا"),
            ("Warehouses","09_انبارها"), ("StockAdjust","10_تعدیل_موجودی"),
            ("SalesInvoice","11_فاکتور_فروش"), ("SalesInvoiceList","12_لیست_فروش"),
            ("PurchaseInvoice","13_فاکتور_خرید"), ("POS","14_صندوق_فروش"),
            ("Customers","15_مشتریان"), ("CustomerEdit","16_ویرایش_مشتری"),
            ("Suppliers","17_تامین_کنندگان"), ("Employees","18_کارکنان"),
            ("Salary","19_حقوق"), ("Attendance","20_حضور_غیاب"),
            ("Reports","21_گزارشها"), ("Settings","22_تنظیمات"), ("Backup","23_پشتیبانگیری"),
        };

        await Task.Delay(1500); // let the shell + dashboard render

        foreach (var (key, file) in pages)
        {
            try
            {
                vm.NavigateCommand.Execute(key);
                await Task.Delay(2200);
                w.UpdateLayout();
                int width = (int)(w.ActualWidth > 0 ? w.ActualWidth : 1600);
                int height = (int)(w.ActualHeight > 0 ? w.ActualHeight : 900);
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                    width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(w);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, file + ".png"));
                enc.Save(fs);
                Log.Information("[SHOT] {File}", file);
            }
            catch (Exception ex) { Log.Warning(ex, "[SHOT] failed {File}", file); }
        }
    }

    private async Task RunSelfTestAsync()
    {
        var sb = new System.Text.StringBuilder();
        void Line(string s) { sb.AppendLine(s); Log.Information("[SELFTEST] " + s); }
        try
        {
            using var scope = _host!.Services.CreateScope();
            var sp = scope.ServiceProvider;
            ((Services.CurrentUserService)sp.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "مدیر سیستم", new[] { "ADMIN" }, Array.Empty<string>());

            var mediator = sp.GetRequiredService<MediatR.IMediator>();
            var products = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IProductRepository>();
            var accounts = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IAccountRepository>();
            var custRepo = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IRepository<SamaHesab.Domain.Entities.CRM.Customer>>();
            var whRepo = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IWarehouseRepository>();

            var prodList = await products.SearchAsync(1, "");
            Line($"Products read: {prodList.Count}");
            var custList = await custRepo.FindAsync(c => c.CompanyId == 1);
            Line($"Customers read: {custList.Count}");
            var whList = await whRepo.GetByCompanyAsync(1);
            Line($"Warehouses read: {whList.Count}");
            var leaf = await accounts.GetLeafAccountsAsync(1);
            Line($"Leaf accounts read: {leaf.Count}");

            // ── Sales invoice ── (use the warehouse that actually holds stock)
            try
            {
                var stockRepo = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IStockItemRepository>();
                var w = whList.First(); SamaHesab.Domain.Entities.Inventory.StockItem? withStock = null;
                foreach (var cand in whList)
                {
                    var st = await stockRepo.GetByWarehouseAsync(cand.Id);
                    var hit = st.FirstOrDefault(s => s.Quantity > 0);
                    if (hit != null) { w = cand; withStock = hit; break; }
                }
                var p = withStock != null ? prodList.First(x => x.Id == withStock.ProductId) : prodList.First();
                var c = custList.First();
                Line($"  (using warehouse '{w.Name}', product '{p.Name}', stock {withStock?.Quantity ?? 0})");
                var cmd = new SamaHesab.Application.Sales.Commands.CreateSalesInvoiceCommand(
                    1, 1, "1403/06/15", c.Id, w.Id, SamaHesab.Domain.Enums.InvoiceType.Sale,
                    "خرده", null, null, "تست خودکار", 0, 0,
                    new System.Collections.Generic.List<SamaHesab.Application.Sales.Commands.SalesInvoiceItemDto>
                    { new(p.Id, 2, p.SalePrice, 0, 9, null, null, null) });
                var r = await mediator.Send(cmd);
                Line(r.Succeeded ? $"SALES INVOICE: PASS (id={r.Value})" : $"SALES INVOICE: FAIL - {r.ErrorMessage}");
            }
            catch (Exception ex) { Line("SALES INVOICE: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Voucher ──
            try
            {
                var a1 = leaf.ElementAt(0).Id; var a2 = leaf.ElementAt(1).Id;
                var cmd = new SamaHesab.Application.Accounting.Commands.CreateVoucherCommand(
                    1, 1, "1403/06/15", 9, "تست سند", null, null, 1,
                    new System.Collections.Generic.List<SamaHesab.Application.Accounting.Commands.VoucherItemDto>
                    {
                        new(1, a1, 1000000, 0, "بدهکار تست", null, null),
                        new(2, a2, 0, 1000000, "بستانکار تست", null, null)
                    });
                var r = await mediator.Send(cmd);
                Line(r.Succeeded ? $"VOUCHER: PASS (id={r.Value})" : $"VOUCHER: FAIL - {r.ErrorMessage}");
            }
            catch (Exception ex) { Line("VOUCHER: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Customer create ──
            try
            {
                var uow = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IUnitOfWork>();
                var entity = SamaHesab.Domain.Entities.CRM.Customer.Create(1, "TST" + DateTime.Now.ToString("HHmmss"),
                    "حقیقی", "تست", "خودکار", null);
                await custRepo.AddAsync(entity);
                await uow.SaveChangesAsync();
                Line($"CUSTOMER CREATE: PASS (id={entity.Id})");
            }
            catch (Exception ex) { Line("CUSTOMER CREATE: EXCEPTION - " + ex.GetBaseException().Message); }
        }
        catch (Exception ex) { Line("SELFTEST FATAL: " + ex.GetBaseException().Message); }

        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Services.AppSettingsStore.AppDataDir, "selftest.txt"), sb.ToString());
        }
        catch { }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ShowFatal(Exception? ex)
    {
        var message = ex?.ToString() ?? "خطای ناشناخته";
        try { Log.Error(ex, "Unhandled startup/runtime error"); } catch { }
        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(Services.AppSettingsStore.AppDataDir, "fatal-error.txt"),
                message);
        }
        catch { }

        MessageBox.Show(
            "خطا در اجرای برنامه:\n\n" + (ex?.Message ?? "نامشخص") +
            "\n\nجزئیات در فایل زیر ذخیره شد:\n" +
            System.IO.Path.Combine(Services.AppSettingsStore.AppDataDir, "fatal-error.txt"),
            "سما حساب - خطا", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static T GetService<T>() where T : notnull =>
        Current is App app && app._host != null
            ? app._host.Services.GetRequiredService<T>()
            : throw new InvalidOperationException("Host not initialized.");
}
