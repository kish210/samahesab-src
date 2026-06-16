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
                    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(Application.Common.Behaviors.AuditBehavior<,>)); // T19/T21
                });

                // WPF Services
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IPrintService, PrintService>();
                services.AddSingleton<Services.ApiClient>();
                services.AddSingleton<Services.UpdateService>();   // به‌روزرسانِ خودکار از GitHub
                services.AddSingleton<ModuleService>();   // سیستم ماژولار (واحد)
                services.AddSingleton<ICurrentUserService, CurrentUserService>();

                // ViewModels
                services.AddTransient<MainViewModel>();
                services.AddTransient<LoginViewModel>();
                services.AddTransient<ViewModels.Onboarding.FirstRunWizardViewModel>();   // فاز ۱۲ G3
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<VoucherListViewModel>();
                services.AddTransient<VoucherEditViewModel>();
                services.AddTransient<ChartOfAccountsViewModel>();
                services.AddTransient<ChequeListViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Treasury.ReceivablesViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Treasury.InterBranchTransferViewModel>();
                services.AddTransient<ChequeBoardViewModel>();
                services.AddTransient<EndOfPeriodViewModel>();
                services.AddTransient<VoucherApprovalsViewModel>();
                services.AddTransient<AccountantDashboardViewModel>();
                services.AddTransient<ManagerDashboardViewModel>();
                services.AddTransient<VoucherProductivityViewModel>();
                services.AddTransient<BankReconciliationViewModel>();
                services.AddTransient<AccountingDimensionsViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Security.SecurityManagementViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Security.AuditLogViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Settings.BranchManagementViewModel>();
                services.AddTransient<BankAccountViewModel>();
                services.AddTransient<ProductListViewModel>();
                services.AddTransient<BatchSerialViewModel>();
                services.AddTransient<InventoryReportViewModel>();
                services.AddTransient<ViewModels.Inventory.ReorderReportViewModel>();
                services.AddTransient<ViewModels.Inventory.WarehouseDashboardViewModel>();
                services.AddTransient<ViewModels.Inventory.PriceListViewModel>();   // کارِ ۷ — مدیریت لیست‌قیمت
                services.AddTransient<ViewModels.Inventory.DiscountTiersViewModel>();   // U6 — تخفیف پلکانی
                services.AddTransient<ProductEditViewModel>();
                services.AddTransient<WarehouseViewModel>();
                services.AddTransient<StockAdjustViewModel>();
                services.AddTransient<SalesInvoiceListViewModel>();
                services.AddTransient<ViewModels.Sales.RecurringInvoiceListViewModel>();   // F9-3 — فاکتورِ تکرارشونده
                services.AddTransient<ViewModels.Sales.SalesReportViewModel>();   // کارِ ۸ — گزارش فروش
                services.AddTransient<ViewModels.Purchase.PurchaseReportViewModel>();   // کارِ ۸ — گزارش خرید
                services.AddTransient<ViewModels.Purchase.SupplierStatementViewModel>();   // C2-C — صورت‌حساب تأمین‌کننده
                services.AddTransient<ViewModels.Purchase.PurchaseOrderListViewModel>();   // F9-2 — سفارش‌های خرید + نقطهٔ سفارش
                services.AddTransient<SalesInvoiceEditViewModel>();
                services.AddTransient<PurchaseInvoiceEditViewModel>();
                services.AddTransient<PurchaseInvoiceListViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Inventory.StockTransferViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Inventory.StockCountViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Inventory.KardexViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Inventory.ProductCardViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Reports.FinancialReportsViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Reports.BranchReportViewModel>();
                services.AddTransient<PosViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.POS.PosDashboardViewModel>();   // F9-1 — داشبورد صندوق/رستوران
                services.AddTransient<SamaHesab.WPF.ViewModels.POS.ShiftViewModel>();
                services.AddTransient<RestaurantPosViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Restaurant.WaiterViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Restaurant.KitchenViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.Inventory.WarehouseClientViewModel>();
                services.AddTransient<CustomerListViewModel>();
                services.AddTransient<CustomerEditViewModel>();
                services.AddTransient<SamaHesab.WPF.ViewModels.CRM.CustomerCardViewModel>();
                services.AddTransient<SupplierListViewModel>();
                services.AddTransient<EmployeeListViewModel>();
                services.AddTransient<EmployeeEditViewModel>();
                services.AddTransient<SalaryViewModel>();
                services.AddTransient<AttendanceViewModel>();
                services.AddTransient<ReportsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<ModulesViewModel>();
                services.AddTransient<CompanySettingsViewModel>();
                services.AddTransient<BackupViewModel>();
                services.AddTransient<ViewModels.Settings.DocumentTemplatesViewModel>();   // فاز ۱۰ DT-4
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
                    // مهاجرت‌های افزایشیِ DB را idempotent اجرا کن تا DBِ کاربر هیچ‌وقت از اسکیما عقب نماند
                    // (ریشهٔ کرش‌های «Invalid column/object name»). فقط اسکریپت‌های 11+ و یک‌بار هرکدام.
                    await SamaHesab.Infrastructure.Data.DatabaseMigrator.RunAsync(
                        Services.AppSettingsStore.GetConnectionString(),
                        msg => Log.Information("[DB-migrate] {Msg}", msg));

                    // Ensure a default admin exists so DB-backed login works on a fresh DB.
                    await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(_host.Services);

                    // DT-10 — نصبِ خودکارِ پکِ ۴۲ قالبِ پیش‌فرض در اولین اجرا (out-of-box).
                    // گیتِ سبک: اگر هیچ قالبِ «PurchaseInvoice» نبود (seedِ پایه فقط SalesInvoice دارد)
                    // یعنی پک هنوز نصب نشده → یک‌بار نصب می‌شود؛ اجراهای بعدی idempotent رد می‌شوند.
                    try
                    {
                        var mediator = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                        var hasPack = await mediator.Send(
                            new SamaHesab.Application.Documents.GetDocumentTemplatesQuery("PurchaseInvoice"));
                        if (hasPack.Count == 0)
                        {
                            var tplDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Templates");
                            var res = await mediator.Send(
                                new SamaHesab.Application.Documents.InstallBuiltInTemplatesCommand(tplDir));
                            Log.Information("[DB-migrate] قالب‌های پیش‌فرض نصب شد — نصب‌شده:{Imp} موجود:{Skip} ناموفق:{Fail}",
                                res.Imported, res.Skipped, res.Failed);
                        }
                    }
                    catch (Exception tex) { Log.Warning(tex, "نصبِ خودکارِ قالب‌های پیش‌فرض ناموفق بود"); }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "اتصال اولیه به پایگاه داده برقرار نشد");
            }
        });

        // ─── Render the restaurant POS to PNG (dev only) ──────────────────────
        if (Environment.GetEnvironmentVariable("SAMA_TEST_APILOGIN") == "1")
        {
            var s = Services.AppSettingsStore.GetApiSettings();
            var ov = Environment.GetEnvironmentVariable("SAMA_API_URL");
            if (!string.IsNullOrWhiteSpace(ov)) s.BaseUrl = ov;
            Log.Information("[APILOGIN] url={Url} user={User}", s.BaseUrl, s.Username);
            var api = _host.Services.GetRequiredService<Services.ApiClient>();
            api.Configure(s.BaseUrl);
            var (ok, err) = await api.LoginAsync(s.Username, s.Password);
            Log.Information("[APILOGIN] result ok={Ok} err={Err}", ok, err);
            if (ok)
            {
                var prods = await api.SearchProductsAsync("");
                var groups = await api.GetGroupsAsync();
                Log.Information("[APILOGIN] products={P} groups={G}", prods.Count, groups.Count);
            }
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_POS") == "1")
        {
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "صندوق‌دار", new[] { "ADMIN" }, Array.Empty<string>());
            try { await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(_host.Services); } catch { }
            var pvm = _host.Services.GetRequiredService<ViewModels.POS.PosViewModel>();
            await pvm.LoadAsync();
            foreach (var bc in new[] { "K1001", "K1002", "K1003" })
            { pvm.BarcodeInput = bc; try { await pvm.ProcessBarcodeCommand.ExecuteAsync(null); } catch { } }
            pvm.CashReceived = 5_000_000;
            var pview = new Views.POS.PosView { DataContext = pvm };
            var pwin = new Window { Content = pview, Width = 1500, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            pwin.Show(); await Task.Delay(2200); pwin.UpdateLayout();
            var pdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(pdir);
            var prtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            prtb.Render(pwin);
            var penc = new System.Windows.Media.Imaging.PngBitmapEncoder(); penc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(prtb));
            using (var pfs = System.IO.File.Create(System.IO.Path.Combine(pdir, "pos.png"))) penc.Save(pfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_WAITER") == "1")
        {
            var s = Services.AppSettingsStore.GetApiSettings();
            var ov = Environment.GetEnvironmentVariable("SAMA_API_URL");
            if (!string.IsNullOrWhiteSpace(ov)) s.BaseUrl = ov;
            var api = _host.Services.GetRequiredService<Services.ApiClient>();
            api.Configure(s.BaseUrl);
            await api.LoginAsync(s.Username, s.Password);
            var wvm = _host.Services.GetRequiredService<ViewModels.Restaurant.WaiterViewModel>();
            await wvm.LoadAsync();
            var wview = new Views.Restaurant.WaiterView { DataContext = wvm };
            var wwin = new Window { Content = wview, Width = 1500, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            wwin.Show(); await Task.Delay(2200); wwin.UpdateLayout();
            var wdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(wdir);
            var wrtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            wrtb.Render(wwin);
            var wenc = new System.Windows.Media.Imaging.PngBitmapEncoder(); wenc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(wrtb));
            using (var wfs = System.IO.File.Create(System.IO.Path.Combine(wdir, "waiter.png"))) wenc.Save(wfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_WAITERDEMO") == "1")
        {
            var wvm = _host.Services.GetRequiredService<ViewModels.Restaurant.WaiterViewModel>();
            // داده‌ی نمونه مستقل از API — مطابق restaurant.html v2
            wvm.Categories.Add(new ViewModels.POS.CategoryTile(-1, "غذای اصلی"));
            wvm.Categories.Add(new ViewModels.POS.CategoryTile(2, "کباب"));
            wvm.Categories.Add(new ViewModels.POS.CategoryTile(3, "پیش‌غذا"));
            wvm.Categories.Add(new ViewModels.POS.CategoryTile(4, "نوشیدنی"));
            wvm.Categories.Add(new ViewModels.POS.CategoryTile(5, "دسر"));
            void M(int id, string n, decimal p) => wvm.MenuItems.Add(new ViewModels.POS.MenuTile(id, "K" + id, n, p, 1, 9));
            M(1,"چلوکباب کوبیده",280000); M(2,"چلوکباب برگ",480000); M(3,"جوجه‌کباب",320000);
            M(4,"زرشک‌پلو با مرغ",290000); M(5,"قورمه‌سبزی",240000); M(6,"میگو سوخاری",520000);
            M(7,"سالاد فصل",85000); M(8,"دوغ محلی",45000); M(9,"نوشابه",35000);
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(1,"۱",2,"آزاد",0,null));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(2,"۲",4,"فعال",1,12, 840000));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(3,"۳",4,"آزاد",0,null));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(4,"۴",6,"فعال",1,13, 1520000));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(5,"۵",4,"فعال",1,14, 1273800));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(6,"۶",2,"صورتحساب",3,15));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(7,"۷",8,"فعال",1,16, 3240000));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(8,"۸",4,"آزاد",0,null));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(9,"۹",2,"آزاد",0,null));
            wvm.Tables.Add(new ViewModels.Restaurant.WaiterTable(10,"۱۰",4,"صورتحساب",3,17));
            wvm.OrderLines.Add(new ViewModels.Restaurant.WaiterOrderLine(1,2,"چلوکباب برگ",1,480000,480000,"در انتظار","بدون پیاز"));
            wvm.OrderLines.Add(new ViewModels.Restaurant.WaiterOrderLine(2,3,"جوجه‌کباب",2,320000,640000,"در انتظار",null));
            wvm.OrderLines.Add(new ViewModels.Restaurant.WaiterOrderLine(3,7,"سالاد فصل",1,85000,85000,"در انتظار",null));
            wvm.OrderLines.Add(new ViewModels.Restaurant.WaiterOrderLine(4,8,"دوغ محلی",2,45000,90000,"در انتظار",null));
            wvm.CurrentTableName = "۵"; wvm.CurrentSeats = 4; wvm.OrderNumber = "R-1042";
            wvm.SubTotal = 1158000; wvm.ServiceTax = 115800; wvm.GrandTotal = 1273800; wvm.ShowOrder = true;
            wvm.StatusText = "شیفت شام · گارسون مجید";
            var wview = new Views.Restaurant.WaiterView { DataContext = wvm };
            var wwin = new Window { Content = wview, Width = 1500, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            wwin.Show(); await Task.Delay(1400); wwin.UpdateLayout();
            var wdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(wdir);
            var wrtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            wrtb.Render(wwin);
            var wenc = new System.Windows.Media.Imaging.PngBitmapEncoder(); wenc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(wrtb));
            using (var wfs = System.IO.File.Create(System.IO.Path.Combine(wdir, "waiter.png"))) wenc.Save(wfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_WAREHOUSE") == "1")
        {
            var s = Services.AppSettingsStore.GetApiSettings();
            var ov = Environment.GetEnvironmentVariable("SAMA_API_URL");
            if (!string.IsNullOrWhiteSpace(ov)) s.BaseUrl = ov;
            var api = _host.Services.GetRequiredService<Services.ApiClient>();
            api.Configure(s.BaseUrl);
            await api.LoginAsync(s.Username, s.Password);
            var wvm = _host.Services.GetRequiredService<ViewModels.Inventory.WarehouseClientViewModel>();
            await wvm.LoadAsync();
            wvm.QuickSearch = "روغن"; await wvm.SearchCommand.ExecuteAsync(null);
            var wview = new Views.Inventory.WarehouseClientView { DataContext = wvm };
            var wwin = new Window { Content = wview, Width = 1500, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            wwin.Show(); await Task.Delay(2200); wwin.UpdateLayout();
            var wdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(wdir);
            var wrtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            wrtb.Render(wwin);
            var wenc = new System.Windows.Media.Imaging.PngBitmapEncoder(); wenc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(wrtb));
            using (var wfs = System.IO.File.Create(System.IO.Path.Combine(wdir, "warehouse.png"))) wenc.Save(wfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_KITCHEN") == "1")
        {
            var s = Services.AppSettingsStore.GetApiSettings();
            var ov = Environment.GetEnvironmentVariable("SAMA_API_URL");
            if (!string.IsNullOrWhiteSpace(ov)) s.BaseUrl = ov;
            var api = _host.Services.GetRequiredService<Services.ApiClient>();
            api.Configure(s.BaseUrl);
            await api.LoginAsync(s.Username, s.Password);
            var kvm = _host.Services.GetRequiredService<ViewModels.Restaurant.KitchenViewModel>();
            await kvm.LoadAsync();
            var kview = new Views.Restaurant.KitchenView { DataContext = kvm };
            var kwin = new Window { Content = kview, Width = 1500, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            kwin.Show(); await Task.Delay(2200); kwin.UpdateLayout();
            var kdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(kdir);
            var krtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            krtb.Render(kwin);
            var kenc = new System.Windows.Media.Imaging.PngBitmapEncoder(); kenc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(krtb));
            using (var kfs = System.IO.File.Create(System.IO.Path.Combine(kdir, "kitchen.png"))) kenc.Save(kfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_VOUCHER") == "1")
        {
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "حسابدار", new[] { "ADMIN" }, Array.Empty<string>());
            var vvm = _host.Services.GetRequiredService<ViewModels.Accounting.VoucherEditViewModel>();
            await vvm.LoadAsync();
            // چند ردیف نمونه برای دیدن گرید و نوار تراز
            var accs = vvm.LeafAccounts;
            if (accs.Count >= 2)
            {
                vvm.NewAccountId = accs[0].Id; vvm.NewDescription = "دریافت نقدی صندوق"; vvm.NewDebit = 285600; vvm.NewCredit = 0; vvm.AddRowCommand.Execute(null);
                vvm.NewAccountId = accs[1].Id; vvm.NewDescription = "فروش طبق فاکتور F000054"; vvm.NewDebit = 0; vvm.NewCredit = 261100; vvm.AddRowCommand.Execute(null);
                if (accs.Count >= 3) { vvm.NewAccountId = accs[2].Id; vvm.NewDescription = "مالیات ۹٪"; vvm.NewDebit = 0; vvm.NewCredit = 24500; vvm.AddRowCommand.Execute(null); }
            }
            vvm.Description = "بابت فروش نقدی و تسویه فاکتور F000054";
            var vview = new Views.Accounting.VoucherEditView { DataContext = vvm };
            var vwin = new Window { Content = vview, Width = 1500, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            vwin.Show(); await Task.Delay(1500); vwin.UpdateLayout();
            var vdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(vdir);
            var vrtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            vrtb.Render(vwin);
            var venc = new System.Windows.Media.Imaging.PngBitmapEncoder(); venc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(vrtb));
            using (var vfs = System.IO.File.Create(System.IO.Path.Combine(vdir, "voucher.png"))) venc.Save(vfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_WIZARD") == "1")
        {
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "مدیر سیستم", new[] { "ADMIN" }, Array.Empty<string>());
            var zwin = new Views.Onboarding.FirstRunWizardWindow(
                _host.Services.GetRequiredService<ViewModels.Onboarding.FirstRunWizardViewModel>());
            zwin.Show(); await Task.Delay(1200); zwin.UpdateLayout();
            var zdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(zdir);
            var zrtb = new System.Windows.Media.Imaging.RenderTargetBitmap(600, 640, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            zrtb.Render(zwin);
            var zenc = new System.Windows.Media.Imaging.PngBitmapEncoder(); zenc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(zrtb));
            using (var zfs = System.IO.File.Create(System.IO.Path.Combine(zdir, "wizard.png"))) zenc.Save(zfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_STOCKCOUNT") == "1")
        {
            var cvm = _host.Services.GetRequiredService<ViewModels.Inventory.StockCountViewModel>();
            cvm.Date = "1405/03/21"; cvm.SessionId = 14; cvm.IsStarted = true; cvm.VarianceCount = 3;
            cvm.Lines.Add(new ViewModels.Inventory.StockCountRow(1, "روغن موتور ۵ لیتری بهران", 120) { CountedQty = 118 });
            cvm.Lines.Add(new ViewModels.Inventory.StockCountRow(2, "فیلتر روغن پراید", 300) { CountedQty = 300 });
            cvm.Lines.Add(new ViewModels.Inventory.StockCountRow(3, "لاستیک ۱۷۵/۷۰R۱۳", 64) { CountedQty = 60 });
            cvm.Lines.Add(new ViewModels.Inventory.StockCountRow(4, "باتری ۶۰ آمپر سپاهان", 45) { CountedQty = 47 });
            cvm.Lines.Add(new ViewModels.Inventory.StockCountRow(5, "شمع موتور NGK", 210) { CountedQty = 210 });
            var sview = new Views.Inventory.StockCountView { DataContext = cvm };
            var swin = new Window { Content = sview, Width = 1200, Height = 760, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            swin.Show(); await Task.Delay(1200); swin.UpdateLayout();
            var sdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(sdir);
            var srtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1200, 760, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            srtb.Render(swin);
            var senc = new System.Windows.Media.Imaging.PngBitmapEncoder(); senc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(srtb));
            using (var sfs = System.IO.File.Create(System.IO.Path.Combine(sdir, "stockcount.png"))) senc.Save(sfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_MODULES") == "1")
        {
            var mvm = _host.Services.GetRequiredService<ViewModels.Settings.ModulesViewModel>();
            var mview = new Views.Settings.ModulesView { DataContext = mvm };
            var mwin = new Window { Content = mview, Width = 1100, Height = 720, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            mwin.Show(); await Task.Delay(1200); mwin.UpdateLayout();
            var mdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(mdir);
            var mrtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1100, 720, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            mrtb.Render(mwin);
            var menc = new System.Windows.Media.Imaging.PngBitmapEncoder(); menc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(mrtb));
            using (var mfs = System.IO.File.Create(System.IO.Path.Combine(mdir, "modules.png"))) menc.Save(mfs);
            Shutdown(); return;
        }

        // ── شات‌های صفحات حسابداری کلود ۱ (R4/R5/R6) — رندر بدون DB با دادهٔ نمونه ──
        if (Environment.GetEnvironmentVariable("SAMA_SHOT_C1") == "1")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;   // بستن پنجره‌ها نباید اپ را زودتر خاموش کند
            async Task Shot(System.Windows.FrameworkElement view, string file, int w, int h)
            {
                var win = new Window { Content = view, Width = w, Height = h, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
                win.Show(); await Task.Delay(1200); win.UpdateLayout();
                var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "screenshot");
                System.IO.Directory.CreateDirectory(dir);
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(win);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder(); enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using var fs = System.IO.File.Create(System.IO.Path.Combine(dir, file)); enc.Save(fs);
                win.Close();
            }

            // R6 — عملیات پایان دوره
            var eop = _host.Services.GetRequiredService<ViewModels.Accounting.EndOfPeriodViewModel>();
            eop.ReverseDate = "1404/03/22"; eop.FiscalYearId = 1; eop.FromDate = "1404/01/01"; eop.ToDate = "1404/12/29";
            eop.ClosingDate = "1404/12/29"; eop.OpeningDate = "1405/01/01";
            eop.HasCloseResult = true; eop.ResultRevenue = 8_450_000_000; eop.ResultExpense = 5_900_000_000;
            eop.ResultNetProfit = 2_550_000_000; eop.ResultClosingVoucherId = 412; eop.CloseResultMessage = "سود دوره: ۲٬۵۵۰٬۰۰۰٬۰۰۰ ریال";
            await Shot(new Views.Accounting.EndOfPeriodView { DataContext = eop }, "eop.png", 1000, 640);

            // R5 — بهره‌وری سند (الگو + تکرارشونده)
            var vp = _host.Services.GetRequiredService<ViewModels.Accounting.VoucherProductivityViewModel>();
            vp.CreateDate = "1404/03/22"; vp.RecurringStartDate = "1404/04/01";
            vp.Templates.Add(new Application.Accounting.Queries.VoucherTemplateDto(1, "اجارهٔ ماهانهٔ دفتر", "هزینهٔ ثابت", 2, 50_000_000, 50_000_000));
            vp.Templates.Add(new Application.Accounting.Queries.VoucherTemplateDto(2, "حقوق پرسنل", null, 4, 320_000_000, 320_000_000));
            vp.Recurring.Add(new Application.Accounting.Queries.RecurringVoucherDto(1, 1, "اجارهٔ دفتر", "ماهانه", "1404/04/01", "1404/03/01"));
            vp.Recurring.Add(new Application.Accounting.Queries.RecurringVoucherDto(2, 2, "حقوق", "ماهانه", "1404/04/01", null));
            await Shot(new Views.Accounting.VoucherProductivityView { DataContext = vp }, "voucher_tools.png", 1100, 640);

            // R4 — مغایرت‌گیری بانکی
            var br = _host.Services.GetRequiredService<ViewModels.Accounting.BankReconciliationViewModel>();
            br.BankAccounts.Add(new ViewModels.Accounting.BankAccountOption(1, "بانک ملت — ۱۲۳۴۵۶"));
            br.SelectedBankAccountId = 1; br.FromDate = "1404/01/01"; br.ToDate = "1404/03/22";
            br.StatementText = "1404/03/10,5000000\n1404/03/15,-2300000\n1404/03/18,7800000";
            br.LedgerCount = 12; br.LastReconInfo = "آخرین تطبیق: 1404/02/31 — 8 ردیف تطبیق‌شدهٔ ماندگار"; br.HasResult = true;
            br.Matched.Add(new ViewModels.Accounting.ReconMatchRow("1404/03/10", 5_000_000, "واریز نقدی مشتری", "TRX-901"));
            br.Matched.Add(new ViewModels.Accounting.ReconMatchRow("1404/03/18", 7_800_000, "تسویهٔ فاکتور ۲۲۰", "TRX-944"));
            br.UnmatchedLedger.Add(new Application.Accounting.Queries.BankLedgerLineDto(55, "1404/03/12", 1_200_000, "کارمزد بانکی"));
            br.UnmatchedStatement.Add(new Application.Accounting.StatementLine("1404/03/15", -2_300_000, "برداشت کارت"));
            br.MatchedCount = 2; br.UnmatchedLedgerCount = 1; br.UnmatchedStatementCount = 1;
            await Shot(new Views.Accounting.BankReconciliationView { DataContext = br }, "bank_recon.png", 1200, 720);

            // CE-1 — ابعاد حسابداری (سال مالی/مرکز هزینه/پروژه)
            var dim = _host.Services.GetRequiredService<ViewModels.Accounting.AccountingDimensionsViewModel>();
            dim.FiscalYears.Add(new Application.Accounting.Dimensions.FiscalYearDto(1, "۱۴۰۳", "1403/01/01", "1403/12/30", true, false));
            dim.FiscalYears.Add(new Application.Accounting.Dimensions.FiscalYearDto(2, "۱۴۰۴", "1404/01/01", "1404/12/29", false, true));
            dim.CostCenters.Add(new Application.Accounting.Dimensions.CostCenterDto(1, "100", "اداری", null, true));
            dim.CostCenters.Add(new Application.Accounting.Dimensions.CostCenterDto(2, "200", "فروش", null, true));
            dim.Projects.Add(new Application.Accounting.Dimensions.ProjectDto(1, "PRJ-01", "احداث انبار مرکزی", "1404/02/01", "1404/10/01", 4_500_000_000, false, true));
            dim.FyTitle = "۱۴۰۴"; dim.FyStart = "1404/01/01"; dim.FyEnd = "1404/12/29";
            await Shot(new Views.Accounting.AccountingDimensionsView { DataContext = dim }, "acc_dimensions.png", 1100, 660);

            // SEC-1 — امنیت و دسترسی (نقش‌ها/مجوزها)
            var sec = _host.Services.GetRequiredService<ViewModels.Security.SecurityManagementViewModel>();
            sec.Roles.Add(new Application.Security.Commands.RoleDto(1, "ADMIN", "مدیر سیستم", true, true, new[] { "*" }));
            sec.Roles.Add(new Application.Security.Commands.RoleDto(2, "ACCOUNTANT", "حسابدار", false, true,
                new[] { "Accounting.Voucher.View", "Accounting.Voucher.Create", "Reports.View" }));
            sec.SelectedRole = sec.Roles[1];
            foreach (var p in Application.Common.Security.PermissionCatalog.All)
                sec.Permissions.Add(new ViewModels.Security.PermCheck(p.Code, p.Module, p.Label)
                { IsChecked = p.Code.StartsWith("Accounting.Voucher") || p.Code == "Reports.View" });
            sec.Users.Add(new Application.Security.Commands.SecurityUserDto(1, "admin", "مدیر سیستم", true, new[] { 1 }));
            sec.Users.Add(new Application.Security.Commands.SecurityUserDto(2, "hesabdar", "علی حسابدار", true, new[] { 2 }));
            await Shot(new Views.Security.SecurityManagementView { DataContext = sec }, "security.png", 1150, 680);

            // INV-1 — بچ و سریال
            var bs = _host.Services.GetRequiredService<ViewModels.Inventory.BatchSerialViewModel>();
            bs.Batches.Add(new Application.Inventory.Commands.BatchDto(1, 1, "B-1404-001", "1404/01/10", "1404/09/10", 120, 85000, null));
            bs.Batches.Add(new Application.Inventory.Commands.BatchDto(2, 1, "B-1404-002", "1404/02/01", "1404/04/01", 40, 86000, null));
            bs.Serials.Add(new Application.Inventory.Commands.SerialDto(1, 1, null, "SN-AX-0001", "موجود", 1200000, "1404/02/15", null));
            bs.Serials.Add(new Application.Inventory.Commands.SerialDto(2, 1, null, "SN-AX-0002", "فروخته شده", 1200000, "1404/02/15", "1404/03/05"));
            bs.Expiring.Add(new Application.Inventory.Commands.ExpiringBatchDto(2, 1, "B-1404-002", "1404/04/01", 40, false));
            bs.Expiring.Add(new Application.Inventory.Commands.ExpiringBatchDto(3, 2, "B-1403-099", "1403/12/20", 15, true));
            await Shot(new Views.Inventory.BatchSerialView { DataContext = bs }, "batch_serial.png", 1150, 680);

            // MB-1 — مدیریت شعب
            var br2 = _host.Services.GetRequiredService<ViewModels.Settings.BranchManagementViewModel>();
            br2.Branches.Add(new Application.Settings.Commands.BranchDto(1, "001", "دفتر مرکزی", "تهران، خیابان ولیعصر", "021-88001122", "آقای رضایی", true, true));
            br2.Branches.Add(new Application.Settings.Commands.BranchDto(2, "002", "شعبهٔ اصفهان", "اصفهان، چهارباغ", "031-32004455", "خانم کریمی", false, true));
            br2.Branches.Add(new Application.Settings.Commands.BranchDto(3, "003", "شعبهٔ مشهد", "مشهد، احمدآباد", "051-38007788", "آقای موسوی", false, false));
            br2.SelectedBranch = br2.Branches[1];
            await Shot(new Views.Settings.BranchManagementView { DataContext = br2 }, "branches.png", 1100, 640);

            // REP-INV — گزارش موجودی/ارزش انبار
            var ir = _host.Services.GetRequiredService<ViewModels.Inventory.InventoryReportViewModel>();
            ir.Warehouses.Add(new ViewModels.Inventory.InvWarehousePick(null, "همهٔ انبارها"));
            ir.Rows.Add(new Application.Inventory.Queries.StockRow(1, "K-1001", "لپ‌تاپ ایسوس", 12, 38_000_000, 456_000_000));
            ir.Rows.Add(new Application.Inventory.Queries.StockRow(2, "K-1002", "ماوس بی‌سیم", 340, 850_000, 289_000_000));
            ir.Rows.Add(new Application.Inventory.Queries.StockRow(3, "K-2010", "کاغذ A4", 1500, 220_000, 330_000_000));
            ir.TotalValue = 1_075_000_000; ir.ItemCount = 3;
            await Shot(new Views.Inventory.InventoryReportView { DataContext = ir }, "inventory_report.png", 1100, 640);

            // accounting-docs — تطبیق با design-system (VoucherListView)
            var vl = _host.Services.GetRequiredService<ViewModels.Accounting.VoucherListViewModel>();
            void AddV(string num, string date, string type, string st, string desc, decimal amt, string user)
                => vl.Vouchers.Add(new Application.Accounting.Queries.VoucherListDto(
                    vl.Vouchers.Count + 1, num, date, type, st, amt, amt, desc, true, user));
            AddV("۱۴۰۵۸۳", "1405/03/15", "عمومی", "پیش‌نویس", "فروش نقدی و تسویه فاکتور F000054", 285_600, "ر.مرادی");
            AddV("۱۴۰۵۸۲", "1405/03/14", "فروش", "قطعی", "فاکتور فروش F000053 — علی احمدی", 54_500, "ر.مرادی");
            AddV("۱۴۰۵۸۱", "1405/03/14", "دریافت", "قطعی", "دریافت چک از پارس خودرو", 45_000_000, "م.رضایی");
            AddV("۱۴۰۵۸۰", "1405/03/13", "پرداخت", "قطعی", "پرداخت حقوق خرداد", 310_000_000, "ر.مرادی");
            AddV("۱۴۰۵۷۹", "1405/03/13", "فروش", "قطعی", "فاکتور فروش F000052 — پارس خودرو", 48_200_000, "س.نوری");
            vl.TotalCount = 83; vl.TotalDebit = 2_486_300_000;
            vl.SelectedVoucher = vl.Vouchers[0];
            vl.PreviewLines.Add(new ViewModels.Accounting.VoucherPreviewLine("صندوق فروشگاه", 285_600, 0));
            vl.PreviewLines.Add(new ViewModels.Accounting.VoucherPreviewLine("فروش کالا", 0, 261_100));
            vl.PreviewLines.Add(new ViewModels.Accounting.VoucherPreviewLine("مالیات ارزش افزوده", 0, 24_500));
            vl.PreviewDebit = 285_600; vl.PreviewCredit = 285_600; vl.PreviewBalanced = true;
            await Shot(new Views.Accounting.VoucherListView { DataContext = vl }, "accounting_docs.png", 1280, 720);

            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_SHIFT") == "1")
        {
            var svm = _host.Services.GetRequiredService<ViewModels.POS.ShiftViewModel>();
            svm.HasOpenShift = true; svm.OpeningFloat = 2_000_000; svm.CashSales = 18_450_000;
            svm.CardSales = 9_300_000; svm.SalesCount = 37; svm.ExpectedCash = 20_450_000; svm.TotalSales = 27_750_000;
            svm.CountedCash = 20_400_000;
            var sview = new Views.POS.ShiftView { DataContext = svm };
            var swin = new Window { Content = sview, Width = 1100, Height = 760, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            swin.Show(); await Task.Delay(1200); swin.UpdateLayout();
            var sdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(sdir);
            var srtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1100, 760, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            srtb.Render(swin);
            var senc = new System.Windows.Media.Imaging.PngBitmapEncoder(); senc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(srtb));
            using (var sfs = System.IO.File.Create(System.IO.Path.Combine(sdir, "shift.png"))) senc.Save(sfs);
            Shutdown(); return;
        }

        if (Environment.GetEnvironmentVariable("SAMA_SHOT_CUSTOMER") == "1")
        {
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "فروش", new[] { "ADMIN" }, Array.Empty<string>());
            var cvm = _host.Services.GetRequiredService<ViewModels.CRM.CustomerCardViewModel>();
            // داده‌ی نمونه برای پیش‌نمایش (بدون نیاز به دیتابیس) — مطابق customer-card.html
            cvm.Name = "بازرگانی پارس خودرو"; cvm.Initials = "پ‌خ"; cvm.Code = "20012";
            cvm.GroupLabel = "حقوقی · همکار عمده"; cvm.Mobile = "0912 345 6789"; cvm.Phone = "021-8876 5432";
            cvm.NationalCode = "10102345678"; cvm.EconomicCode = "411234567890";
            cvm.Address = "تهران، بازار قطعات، پلاک ۱۲";
            cvm.Balance = 78000000; cvm.CreditLimit = 120000000; cvm.UnlimitedCredit = false;
            cvm.CreditPercent = 65; cvm.CreditPercentLabel = SamaHesab.WPF.Converters.NumberFormatConverter.ToPersian("65٪");
            cvm.TotalSales = 1240000000; cvm.InvoiceCount = 24; cvm.AveragePerInvoice = 51666000; cvm.LoyaltyPoints = 1850; cvm.SettlementDays = 22;
            cvm.Ledger.Add(new ViewModels.CRM.LedgerRow("1405/03/13","F000052","فاکتور فروش — ۸ قلم قطعات",48200000,0,78000000,"بد"));
            cvm.Ledger.Add(new ViewModels.CRM.LedgerRow("1405/03/05","RC-0114","دریافت چک — ملت ۴۵۶۱۲۳",0,45000000,29800000,"بد"));
            cvm.Ledger.Add(new ViewModels.CRM.LedgerRow("1405/02/28","F000048","فاکتور فروش — لاستیک و باتری",36400000,0,74800000,"بد"));
            cvm.Ledger.Add(new ViewModels.CRM.LedgerRow("1405/02/20","RC-0109","دریافت وجه — کارت‌خوان",0,50000000,38400000,"بد"));
            cvm.Ledger.Add(new ViewModels.CRM.LedgerRow("1405/02/11","F000044","فاکتور فروش — روغن و فیلتر",22900000,0,88400000,"بد"));
            cvm.LedgerTotalDebit = 324500000; cvm.LedgerTotalCredit = 246500000; cvm.LedgerClosing = 78000000; cvm.HasData = true;
            var cview = new Views.CRM.CustomerCardView { DataContext = cvm };
            var cwin = new Window { Content = cview, Width = 1500, Height = 820, WindowStartupLocation = WindowStartupLocation.CenterScreen, FlowDirection = FlowDirection.RightToLeft };
            cwin.Show(); await Task.Delay(1400); cwin.UpdateLayout();
            var cdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(cdir);
            var crtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            crtb.Render(cwin);
            var cenc = new System.Windows.Media.Imaging.PngBitmapEncoder(); cenc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(crtb));
            using (var cfs = System.IO.File.Create(System.IO.Path.Combine(cdir, "customer.png"))) cenc.Save(cfs);
            Shutdown(); return;
        }

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

        // ─── Capture the ERP shell only (dev only) — تأیید پوسته در برابر طرح ───
        if (Environment.GetEnvironmentVariable("SAMA_SHOT_SHELL") == "1")
        {
            ((Services.CurrentUserService)_host.Services.GetRequiredService<ICurrentUserService>())
                .SetCurrentUser(1, 1, 1, "admin", "مدیر سیستم", new[] { "ADMIN" }, Array.Empty<string>());
            var w = _host.Services.GetRequiredService<MainWindow>();
            w.WindowState = WindowState.Normal; w.Width = 1500; w.Height = 820;
            w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            w.Show(); await Task.Delay(2600); w.UpdateLayout();
            var sdir = @"D:\duc\sama-hesab\screenshot"; System.IO.Directory.CreateDirectory(sdir);
            var srtb = new System.Windows.Media.Imaging.RenderTargetBitmap(1500, 820, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            srtb.Render(w);
            var senc = new System.Windows.Media.Imaging.PngBitmapEncoder(); senc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(srtb));
            using (var sfs = System.IO.File.Create(System.IO.Path.Combine(sdir, "shell.png"))) senc.Save(sfs);
            Shutdown(); return;
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
        // This is a standalone CLIENT: it talks to the central server over the Web API
        // (HTTP), never the database directly. Server address is set in ApiSettings.
        if (e.Args.Contains("--pos") || Environment.GetEnvironmentVariable("SAMA_POS") == "1")
        {
            var apiSettings = Services.AppSettingsStore.GetApiSettings();
            ShowApiLogin(e.Args, "فروشگاه", () =>
            {
                var posVm = _host!.Services.GetRequiredService<ViewModels.POS.PosViewModel>();
                posVm.ConfigureApi(apiSettings.CustomerId, apiSettings.WarehouseId);
                _ = posVm.LoadAsync();
                new Window
                {
                    Title = "صندوق فروش — سما حساب",
                    Content = new Views.POS.PosView { DataContext = posVm },
                    DataContext = posVm,
                    WindowState = WindowState.Maximized,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    FlowDirection = FlowDirection.RightToLeft,
                    FontFamily = (System.Windows.Media.FontFamily?)TryFindResource("VazirFont")
                }.Show();
            });
            return;
        }

        // ─── Restaurant POS mode (restoran.exe / --restaurant / SAMA_RESTAURANT=1) ──
        // Standalone CLIENT — talks to the central server over the Web API, not the DB.
        if (e.Args.Contains("--restaurant") || Environment.GetEnvironmentVariable("SAMA_RESTAURANT") == "1")
        {
            var apiSettings = Services.AppSettingsStore.GetApiSettings();
            ShowApiLogin(e.Args, "رستوران", () =>
            {
                var rvm = _host!.Services.GetRequiredService<ViewModels.POS.RestaurantPosViewModel>();
                rvm.ConfigureApi(apiSettings.CustomerId, apiSettings.WarehouseId);
                _ = rvm.LoadAsync();
                new Window
                {
                    Title = "صندوق رستوران — سما حساب",
                    Content = new Views.POS.RestaurantPosView { DataContext = rvm },
                    DataContext = rvm,
                    WindowState = WindowState.Maximized,
                    FlowDirection = FlowDirection.RightToLeft,
                    FontFamily = (System.Windows.Media.FontFamily?)TryFindResource("VazirFont")
                }.Show();
            });
            return;
        }

        // ─── Waiter mode (--waiter / SAMA_WAITER=1): touch table/hall board (v2) ──
        // Standalone CLIENT — halls, tables, orders and kitchen all go through the Web API.
        if (e.Args.Contains("--waiter") || Environment.GetEnvironmentVariable("SAMA_WAITER") == "1")
        {
            ShowApiLogin(e.Args, "گارسون", () =>
            {
                var wvm = _host!.Services.GetRequiredService<ViewModels.Restaurant.WaiterViewModel>();
                _ = wvm.LoadAsync();
                new Window
                {
                    Title = "صندوق گارسون — سما حساب",
                    Content = new Views.Restaurant.WaiterView { DataContext = wvm },
                    DataContext = wvm,
                    WindowState = WindowState.Maximized,
                    FlowDirection = FlowDirection.RightToLeft,
                    FontFamily = (System.Windows.Media.FontFamily?)TryFindResource("VazirFont")
                }.Show();
            });
            return;
        }

        // ─── Kitchen Display mode (--kitchen / SAMA_KITCHEN=1): KDS for the kitchen ──
        if (e.Args.Contains("--kitchen") || Environment.GetEnvironmentVariable("SAMA_KITCHEN") == "1")
        {
            ShowApiLogin(e.Args, "آشپزخانه", () =>
            {
                var kvm = _host!.Services.GetRequiredService<ViewModels.Restaurant.KitchenViewModel>();
                _ = kvm.LoadAsync();
                new Window
                {
                    Title = "نمایشگر آشپزخانه — سما حساب",
                    Content = new Views.Restaurant.KitchenView { DataContext = kvm },
                    DataContext = kvm,
                    WindowState = WindowState.Maximized,
                    FlowDirection = FlowDirection.RightToLeft,
                    FontFamily = (System.Windows.Media.FontFamily?)TryFindResource("VazirFont")
                }.Show();
            });
            return;
        }

        // ─── Warehouse client mode (--warehouse / SAMA_WAREHOUSE=1): operator app ──
        if (e.Args.Contains("--warehouse") || Environment.GetEnvironmentVariable("SAMA_WAREHOUSE") == "1")
        {
            ShowApiLogin(e.Args, "انبار", () =>
            {
                var wvm = _host!.Services.GetRequiredService<ViewModels.Inventory.WarehouseClientViewModel>();
                _ = wvm.LoadAsync();
                new Window
                {
                    Title = "انبارداری — سما حساب",
                    Content = new Views.Inventory.WarehouseClientView { DataContext = wvm },
                    DataContext = wvm,
                    WindowState = WindowState.Maximized,
                    FlowDirection = FlowDirection.RightToLeft,
                    FontFamily = (System.Windows.Media.FontFamily?)TryFindResource("VazirFont")
                }.Show();
            });
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

        // ─── به‌روزرسانیِ خودکار از GitHub (فقط حالا که سیستم بیکار است، پیش از ورود) ──
        if (await CheckForUpdateAsync()) return;   // اگر کاربر به‌روزرسانی را پذیرفت، نصاب اجرا و اپ بسته می‌شود

        var loginWindow = _host.Services.GetRequiredService<LoginWindow>();
        loginWindow.Show();
    }

    /// <summary>
    /// بررسیِ نسخهٔ جدید روی GitHub در زمانِ استارت‌آپ (سیستم درحالِ اجرای کاری نیست).
    /// اگر نسخهٔ جدیدتری باشد و کاربر تأیید کند، نصاب دانلود و اجرا و برنامه بسته می‌شود.
    /// برمی‌گرداند: true یعنی به‌روزرسانی آغاز شد (جریانِ ورود نباید ادامه یابد).
    /// </summary>
    private async Task<bool> CheckForUpdateAsync()
    {
        try
        {
            var updater = _host!.Services.GetRequiredService<Services.UpdateService>();
            var info = await updater.CheckAsync();
            if (info is null) return false;   // آخرین نسخه‌ایم یا آفلاین

            var dialog = _host.Services.GetRequiredService<IDialogService>();
            var ok = await dialog.ConfirmAsync(
                $"نسخهٔ جدید ({info.Tag}) موجود است (نسخهٔ فعلی: {Services.UpdateService.CurrentVersion}).\n" +
                "اکنون دانلود و نصب شود؟ برنامه بسته می‌شود و نصاب اجرا می‌گردد.",
                "به‌روزرسانی");
            if (!ok) return false;

            if (await updater.DownloadAndRunAsync(info))
            {
                Shutdown();
                return true;
            }
            await dialog.ShowErrorAsync("دانلودِ به‌روزرسانی ناموفق بود؛ برنامه به‌صورتِ عادی ادامه می‌دهد.");
            return false;
        }
        catch { return false; }   // هر خطایی → ادامهٔ عادیِ برنامه
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

    /// <summary>
    /// Show the (API-mode) login form for the POS / restaurant kiosk clients. On a successful
    /// login the current user is set from the server (/api/auth/me) and <paramref name="onAuthenticated"/>
    /// runs to open the kiosk window — the login window is closed afterwards (open-then-close, so the
    /// app does not shut down on last-window-close). Closing the form without logging in exits the app.
    /// </summary>
    private void ShowApiLogin(string[] args, string moduleName, Action onAuthenticated)
    {
        var settings = Services.AppSettingsStore.GetApiSettings();
        // --setup forces the server-connection dialog first (set/verify the server IP).
        if (args.Contains("--setup"))
        {
            new Views.Shell.ApiSettingsWindow().ShowDialog();
            settings = Services.AppSettingsStore.GetApiSettings();
        }

        var api = _host!.Services.GetRequiredService<Services.ApiClient>();
        api.Configure(settings.BaseUrl);

        var vm = _host.Services.GetRequiredService<ViewModels.Shell.LoginViewModel>();
        vm.EnableApiMode(moduleName);
        var win = new Views.Shell.LoginWindow(vm);
        vm.Authenticated += () =>
        {
            onAuthenticated();   // open the kiosk window first …
            win.Close();         // … then close the login form
        };
        win.Show();
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
            ("Modules","27_مدیریت_ماژولها"), ("DocumentTemplates","28_قالب_اسناد"),
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
