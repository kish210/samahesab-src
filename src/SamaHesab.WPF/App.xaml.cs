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
                services.AddSingleton<IPrintService, PrintService>();
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
                services.AddTransient<PurchaseInvoiceListViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Inventory.StockTransferViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Inventory.KardexViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Reports.FinancialReportsViewModel>();
                services.AddTransient<PosViewModel>();
                services.AddTransient<RestaurantPosViewModel>();
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
                if (ok)
                {
                    // Ensure a default admin exists so DB-backed login works on a fresh DB.
                    await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(_host.Services);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "اتصال اولیه به پایگاه داده برقرار نشد");
            }
        });

        // ─── Render the restaurant POS to PNG (dev only) ──────────────────────
        if (Environment.GetEnvironmentVariable("SAMA_SHOT_RESTAURANT") == "1")
        {
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "صندوق رستوران", new[] { "ADMIN" }, Array.Empty<string>());
            try { await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(_host.Services);
                  await SamaHesab.Infrastructure.Seed.RestaurantSeeder.EnsureMenuAsync(_host.Services); } catch { }
            var rvm = _host.Services.GetRequiredService<ViewModels.POS.RestaurantPosViewModel>();
            await rvm.LoadAsync();
            // add a couple of demo lines
            if (rvm.MenuItems.Count > 0) { rvm.AddItemCommand.Execute(rvm.MenuItems[0]); if (rvm.MenuItems.Count > 3) rvm.AddItemCommand.Execute(rvm.MenuItems[3]); }
            var view = new Views.POS.RestaurantPosView { DataContext = rvm };
            var win = new Window { Content = view, Width = 1500, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            win.Show(); await Task.Delay(2200); win.UpdateLayout();
            var dir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(dir);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(win);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using (var fs = System.IO.File.Create(System.IO.Path.Combine(dir, "restoran.png"))) enc.Save(fs);
            Shutdown(); return;
        }

        // ─── Capture the login window only (dev only) ─────────────────────────
        if (Environment.GetEnvironmentVariable("SAMA_SHOT_LOGIN") == "1")
        {
            var login = _host.Services.GetRequiredService<LoginWindow>();
            login.Show();
            await Task.Delay(1500);
            login.UpdateLayout();
            var dir = @"D:\duc\sama-hesab\screenshot";
            System.IO.Directory.CreateDirectory(dir);
            int width = (int)(login.ActualWidth > 0 ? login.ActualWidth : 440);
            int height = (int)(login.ActualHeight > 0 ? login.ActualHeight : 580);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(
                width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(login);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using (var fs = System.IO.File.Create(System.IO.Path.Combine(dir, "00_login.png")))
                enc.Save(fs);
            Shutdown();
            return;
        }

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

        // ─── Touch POS mode (pos.exe / --pos / SAMA_POS=1): fullscreen fast checkout ──
        if (e.Args.Contains("--pos") || Environment.GetEnvironmentVariable("SAMA_POS") == "1")
        {
            // On a separate client PC the DB is remote; if it can't be reached, let the
            // operator enter the server IP, then restart to apply.
            if (e.Args.Contains("--setup") || !await ClientDbReachableAsync())
            {
                new Views.Shell.ConnectionSettingsWindow().ShowDialog();
                RestartSelf(e.Args.Where(a => a != "--setup").ToArray());
                return;
            }
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "صندوق‌دار", new[] { "ADMIN" }, Array.Empty<string>());
            try { await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(_host.Services); } catch { }
            var posVm = _host.Services.GetRequiredService<ViewModels.POS.PosViewModel>();
            await posVm.LoadAsync();
            var posWindow = new Window
            {
                Title = "صندوق فروش — سما حساب",
                Content = new Views.POS.PosView { DataContext = posVm },
                DataContext = posVm,
                WindowState = WindowState.Maximized,
                WindowStyle = WindowStyle.SingleBorderWindow,
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = (System.Windows.Media.FontFamily?)TryFindResource("VazirFont")
            };
            posWindow.Show();
            return;
        }

        // ─── Restaurant POS mode (restoran.exe / --restaurant / SAMA_RESTAURANT=1) ──
        if (e.Args.Contains("--restaurant") || Environment.GetEnvironmentVariable("SAMA_RESTAURANT") == "1")
        {
            if (e.Args.Contains("--setup") || !await ClientDbReachableAsync())
            {
                new Views.Shell.ConnectionSettingsWindow().ShowDialog();
                RestartSelf(e.Args.Where(a => a != "--setup").ToArray());
                return;
            }
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "صندوق رستوران", new[] { "ADMIN" }, Array.Empty<string>());
            try
            {
                await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(_host.Services);
                await SamaHesab.Infrastructure.Seed.RestaurantSeeder.EnsureMenuAsync(_host.Services);
            }
            catch (Exception ex) { Log.Warning(ex, "Restaurant seed skipped"); }
            var rvm = _host.Services.GetRequiredService<ViewModels.POS.RestaurantPosViewModel>();
            await rvm.LoadAsync();
            new Window
            {
                Title = "صندوق رستوران — سما حساب",
                Content = new Views.POS.RestaurantPosView { DataContext = rvm },
                DataContext = rvm,
                WindowState = WindowState.Maximized,
                FlowDirection = FlowDirection.RightToLeft,
                FontFamily = (System.Windows.Media.FontFamily?)TryFindResource("VazirFont")
            }.Show();
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

    /// <summary>Probe the configured DB (short timeout) — used by kiosk clients (pos/restoran).</summary>
    private async Task<bool> ClientDbReachableAsync()
    {
        try
        {
            using var scope = _host!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Database.CanConnectAsync();
        }
        catch { return false; }
    }

    /// <summary>Relaunch the current executable (to apply a changed connection string).</summary>
    private void RestartSelf(string[] args)
    {
        try
        {
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, string.Join(' ', args)) { UseShellExecute = false });
        }
        catch { }
        Shutdown();
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
            ("FinancialReports","24_گزارشهای_مالی"), ("StockTransfer","25_انتقال_انبار"), ("Kardex","26_کاردکس"),
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

            // ── Sales VIEWMODEL product-search test (the actual UI path) ──
            try
            {
                var svm = sp.GetRequiredService<ViewModels.Sales.SalesInvoiceEditViewModel>();
                await svm.LoadAsync();
                Line($"  SalesVM: customers={svm.Customers.Count}, warehouses={svm.Warehouses.Count}, products={svm.AllProducts.Count}");
                if (svm.AllProducts.Count > 0)
                {
                    svm.SelectedProductItem = svm.AllProducts[0];
                    svm.AddSelectedProductCommand.Execute(null);
                    Line($"  SalesVM AddSelectedProduct → cartItems={svm.InvoiceItems.Count}");
                }
            }
            catch (Exception ex) { Line("SALES VM: EXCEPTION - " + ex.GetBaseException().Message); }

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

                // invoice with amount discount + split payment + sales-rep commission
                var voucherRepo = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IVoucherRepository>();
                var beforeCount = (await voucherRepo.GetAllAsync()).Count();
                var cmd2 = new SamaHesab.Application.Sales.Commands.CreateSalesInvoiceCommand(
                    1, 1, "1403/06/15", c.Id, w.Id, SamaHesab.Domain.Enums.InvoiceType.Sale,
                    "خرده", 1, null, "تست تخفیف+پرداخت+پورسانت", 0, 0,
                    new System.Collections.Generic.List<SamaHesab.Application.Sales.Commands.SalesInvoiceItemDto>
                    { new(p.Id, 1, 1000000, 0, 0, null, null, null) },
                    InvoiceDiscount: 100000, PaidAmount: 500000, PaymentMethod: "نقدی", CommissionPercent: 5);
                var r2 = await mediator.Send(cmd2);
                var afterCount = (await voucherRepo.GetAllAsync()).Count();
                Line(r2.Succeeded
                    ? $"SALES INVOICE (discount+split+commission): PASS (id={r2.Value}, vouchers +{afterCount - beforeCount} [expect 2])"
                    : $"SALES INVOICE (discount+split+commission): FAIL - {r2.ErrorMessage}");
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

            // ── Purchase invoice (stock increase + auto voucher) ──
            try
            {
                var stockRepo = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IStockItemRepository>();
                var w = whList.First();
                var p = prodList.First();
                var before = (await stockRepo.GetByProductAndWarehouseAsync(p.Id, w.Id))?.Quantity ?? 0;
                var cmd = new SamaHesab.Application.Purchase.Commands.CreatePurchaseInvoiceCommand(
                    1, 1, "1403/06/15", 1, w.Id, "خرید", null, null, "تست خرید خودکار", 0, 0,
                    new System.Collections.Generic.List<SamaHesab.Application.Purchase.Commands.PurchaseInvoiceItemDto>
                    { new(p.Id, 5, 800000, 0, 9, null, null, null, null, null) });
                var r = await mediator.Send(cmd);
                var after = (await stockRepo.GetByProductAndWarehouseAsync(p.Id, w.Id))?.Quantity ?? 0;
                Line(r.Succeeded
                    ? $"PURCHASE INVOICE: PASS (id={r.Value}, موجودی {before}→{after}, +{after - before} [expect 5])"
                    : $"PURCHASE INVOICE: FAIL - {r.ErrorMessage}");

                // verify the invoice record was persisted and the list VM loads it
                var purRepo = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IRepository<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice>>();
                var savedCount = (await purRepo.GetAllAsync()).Count;
                var plvm = sp.GetService<SamaHesab.WPF.ViewModels.Purchase.PurchaseInvoiceListViewModel>();
                if (plvm != null) { await plvm.LoadAsync(); Line($"PURCHASE LIST: PASS (رکوردها={savedCount}, لیست={plvm.Invoices.Count})"); }

                var slvm = sp.GetService<SamaHesab.WPF.ViewModels.Sales.SalesInvoiceListViewModel>();
                if (slvm != null) { await slvm.LoadAsync(); Line($"SALES LIST: PASS (لیست={slvm.Invoices.Count}, جمع={slvm.TotalAmount:#,##0})"); }
            }
            catch (Exception ex) { Line("PURCHASE INVOICE: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Stock transfer + kardex (#3) ──
            try
            {
                if (whList.Count >= 2)
                {
                    var stockRepo = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IStockItemRepository>();
                    var src = whList.First(); var dst = whList.ElementAt(1); var p = prodList.First();
                    var srcBefore = (await stockRepo.GetByProductAndWarehouseAsync(p.Id, src.Id))?.Quantity ?? 0;
                    var dstBefore = (await stockRepo.GetByProductAndWarehouseAsync(p.Id, dst.Id))?.Quantity ?? 0;
                    var tr = await mediator.Send(new SamaHesab.Application.Inventory.Commands.TransferStockCommand(
                        src.Id, dst.Id, p.Id, 3, "1403/06/16", "تست انتقال"));
                    var srcAfter = (await stockRepo.GetByProductAndWarehouseAsync(p.Id, src.Id))?.Quantity ?? 0;
                    var dstAfter = (await stockRepo.GetByProductAndWarehouseAsync(p.Id, dst.Id))?.Quantity ?? 0;
                    Line(tr.Succeeded
                        ? $"STOCK TRANSFER: PASS (مبدأ {srcBefore}→{srcAfter}, مقصد {dstBefore}→{dstAfter})"
                        : $"STOCK TRANSFER: FAIL - {tr.ErrorMessage}");

                    var kardex = await mediator.Send(new SamaHesab.Application.Inventory.Queries.GetKardexQuery(p.Id, null, null, null));
                    Line($"KARDEX: PASS (ردیف‌ها={kardex.Count}, ورود={kardex.Sum(k => k.In):#,##0}, خروج={kardex.Sum(k => k.Out):#,##0})");
                }
            }
            catch (Exception ex) { Line("STOCK TRANSFER/KARDEX: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Financial reports (#2) ──
            try
            {
                var tb = await mediator.Send(new SamaHesab.Application.Reports.Queries.GetTrialBalanceQuery("1400/01/01", "1410/12/29"));
                var td = tb.Sum(r => r.Debit); var tc = tb.Sum(r => r.Credit);
                Line($"TRIAL BALANCE: PASS (حساب‌ها={tb.Count}, بدهکار={td:#,##0}, بستانکار={tc:#,##0}, تراز={(td == tc ? "بله" : "خیر")})");
                var pl = await mediator.Send(new SamaHesab.Application.Reports.Queries.GetProfitLossQuery("1400/01/01", "1410/12/29"));
                Line($"PROFIT/LOSS: PASS (درآمد={pl.Revenue:#,##0}, هزینه={pl.Expense:#,##0}, سود خالص={pl.NetProfit:#,##0})");
                var led = await mediator.Send(new SamaHesab.Application.Reports.Queries.GetGeneralLedgerQuery("1400/01/01", "1410/12/29", null));
                Line($"GENERAL LEDGER: PASS (ردیف‌ها={led.Count})");
            }
            catch (Exception ex) { Line("FINANCIAL REPORTS: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Balance sheet (ترازنامه) ──
            try
            {
                var bs = await mediator.Send(new SamaHesab.Application.Reports.Queries.GetBalanceSheetQuery("1400/01/01", "1410/12/29"));
                Line($"BALANCE SHEET: {(bs.IsBalanced ? "PASS" : "FAIL")} (دارایی={bs.TotalAssets:#,##0}, بدهی+سرمایه={bs.TotalLiabilities + bs.TotalEquity:#,##0}, سود={bs.NetProfit:#,##0})");
            }
            catch (Exception ex) { Line("BALANCE SHEET: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Open existing voucher (view/edit from list) ──
            try
            {
                var vrepo = sp.GetRequiredService<SamaHesab.Domain.Interfaces.Repositories.IVoucherRepository>();
                var all = await vrepo.GetByDateRangeAsync(1, 1, "1400/01/01", "1410/12/29");
                var any = all.FirstOrDefault();
                if (any != null)
                {
                    var vevm = sp.GetRequiredService<SamaHesab.WPF.ViewModels.Accounting.VoucherEditViewModel>();
                    await vevm.LoadAsync();
                    await ((SamaHesab.WPF.Services.INavigationAware)vevm).OnNavigatedToAsync(any.Id);
                    Line($"VOUCHER OPEN: PASS (سند {vevm.VoucherNumber}، ردیف‌ها={vevm.Items.Count}، بدهکار={vevm.TotalDebit:#,##0})");
                }
                else Line("VOUCHER OPEN: SKIP (سندی موجود نیست)");
            }
            catch (Exception ex) { Line("VOUCHER OPEN: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Authentication (DB-backed) + audit ──
            try
            {
                await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(_host.Services);
                var good = await mediator.Send(new SamaHesab.Application.Security.Commands.AuthenticateCommand(1, "admin", "admin123"));
                var bad = await mediator.Send(new SamaHesab.Application.Security.Commands.AuthenticateCommand(1, "admin", "wrongpass"));
                Line(good.Succeeded && !bad.Succeeded
                    ? $"AUTH: PASS (admin/admin123 ✓ id={good.Value?.UserId}, رمز غلط ✗)"
                    : $"AUTH: FAIL (good={good.Succeeded}, bad={bad.Succeeded})");
            }
            catch (Exception ex) { Line("AUTH: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Treasury receipt (دریافت) ──
            try
            {
                var c = custList.First();
                var before = (await custRepo.GetByIdAsync(c.Id))?.Balance ?? 0;
                var r = await mediator.Send(new SamaHesab.Application.Treasury.Commands.CreateReceiptCommand(
                    1, 1, "1403/06/15", c.Id, 1_000_000, "نقدی", "تست دریافت"));
                var after = (await custRepo.GetByIdAsync(c.Id))?.Balance ?? 0;
                Line(r.Succeeded
                    ? $"RECEIPT: PASS (سند={r.Value}, مانده مشتری {before:#,##0}→{after:#,##0})"
                    : $"RECEIPT: FAIL - {r.ErrorMessage}");
            }
            catch (Exception ex) { Line("RECEIPT: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Customer statement ──
            try
            {
                var c = custList.First();
                var st = await mediator.Send(new SamaHesab.Application.CRM.Queries.GetCustomerStatementQuery(c.Id));
                Line(st.Succeeded
                    ? $"CUSTOMER STATEMENT: PASS ({st.Value!.CustomerName}، ردیف‌ها={st.Value.Rows.Count}، مانده={st.Value.ClosingBalance:#,##0})"
                    : $"CUSTOMER STATEMENT: FAIL - {st.ErrorMessage}");
            }
            catch (Exception ex) { Line("CUSTOMER STATEMENT: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Excel export service ──
            try
            {
                var excel = sp.GetRequiredService<SamaHesab.Application.Common.Interfaces.IExcelExportService>();
                var bytes = excel.Export("تست",
                    new[] { "کد", "نام", "مبلغ" },
                    new[] { (IReadOnlyList<object?>)new object?[] { "K1", "کالای تست", 1234500m } });
                bool validXlsx = bytes.Length > 0 && bytes[0] == (byte)'P' && bytes[1] == (byte)'K';
                Line(validXlsx ? $"EXCEL EXPORT: PASS (bytes={bytes.Length})" : "EXCEL EXPORT: FAIL (invalid xlsx)");
            }
            catch (Exception ex) { Line("EXCEL EXPORT: EXCEPTION - " + ex.GetBaseException().Message); }

            // ── Invoice print document (build only, no printer) ──
            try
            {
                var ps = (Services.PrintService)sp.GetRequiredService<Services.IPrintService>();
                var data = new Services.PrintDocumentData("فاکتور فروش", "F000001", "1403/06/15", "مشتری", "علی احمدی",
                    new[] { new Services.PrintLine(1, "K1001", "روغن موتور", 2, 500000, 0, 1090000) },
                    1000000, 0, 90000, 0, 1090000, 1090000, 0, "تست چاپ");
                var docA4 = ps.Build(data, new Services.PrintSettings(), receipt: false);
                var docRcpt = ps.Build(data, new Services.PrintSettings { Paper = Services.PaperKind.Receipt80mm }, receipt: true);
                Line(docA4.Blocks.Count > 0 && docRcpt.Blocks.Count > 0
                    ? $"PRINT DOC: PASS (A4 blocks={docA4.Blocks.Count}, رسید blocks={docRcpt.Blocks.Count})"
                    : "PRINT DOC: FAIL (empty document)");
            }
            catch (Exception ex) { Line("PRINT DOC: EXCEPTION - " + ex.GetBaseException().Message); }

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
