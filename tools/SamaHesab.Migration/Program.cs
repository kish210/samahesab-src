using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Import;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Migration;

Console.OutputEncoding = System.Text.Encoding.UTF8;
try { Console.InputEncoding = System.Text.Encoding.UTF8; } catch { /* در برخی ترمینال‌ها قابل‌تنظیم نیست */ }
Console.WriteLine("══════════════════════════════════════════════");
Console.WriteLine("  ابزارِ مهاجرتِ سما حساب — ورودِ داده از نرم‌افزارِ دیگر");
Console.WriteLine("══════════════════════════════════════════════");

// رشتهٔ اتصال (پیش‌فرض = همان DBِ سما حساب)؛ نمونهٔ SQLِ کارا خودکار پیدا می‌شود.
const string def = "Server=.\\SQLEXPRESS;Database=SamaHesab;Trusted_Connection=True;" +
                   "TrustServerCertificate=True;Encrypt=False;MultipleActiveResultSets=True;Connect Timeout=5;";
string cs;
try { cs = await SqlInstanceProbe.ResolveAsync(def, m => Console.WriteLine("  " + m)); }
catch { cs = def; }

var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = cs })
    .Build();

var services = new ServiceCollection();
services.AddLogging();   // ILogger برای LoggingBehaviorِ pipelineِ MediatR
services.AddApplication();
services.AddInfrastructure(config);
services.AddScoped<ICurrentUserService, ConsoleUser>();
var sp = services.BuildServiceProvider();

// اطمینان از وجودِ پایگاه‌داده/داده‌های پایه (نصبِ تازه هم کار کند).
try { await DatabaseMigrator.RunAsync(cs); } catch (Exception ex) { Console.WriteLine("هشدارِ آماده‌سازیِ DB: " + ex.GetBaseException().Message); }

using var scope = sp.CreateScope();
var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
var excel = scope.ServiceProvider.GetRequiredService<IExcelImportService>();

while (true)
{
    Console.WriteLine();
    Console.WriteLine("نرم‌افزارِ مبدأ:  [1] حساب‌فا   [2] سپیدار   [3] هلو   [4] اکسلِ استاندارد     ([0] خروج)");
    Console.Write("> ");
    var src = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(src) || src == "0") break;

    Console.WriteLine("نوعِ داده:  [1] اشخاص (مشتری+تأمین‌کننده)   [2] کالا   [3] فقط مشتری   [4] فقط تأمین‌کننده");
    Console.Write("> ");
    var ent = Console.ReadLine()?.Trim();

    Console.Write("مسیرِ فایلِ اکسل: ");
    var path = Console.ReadLine()?.Trim().Trim('"');
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("  ✗ فایل یافت نشد."); continue; }

    try
    {
        var rows = excel.ReadRows(path);
        Console.WriteLine($"  {rows.Count} سطر خوانده شد. در حال ورود…");
        ImportResult res = ent switch
        {
            "2" => await mediator.Send(new ImportProductsCommand(rows)),
            "3" => await mediator.Send(new ImportCustomersCommand(rows)),
            "4" => await mediator.Send(new ImportSuppliersCommand(rows)),
            _   => await mediator.Send(new ImportPersonsCommand(rows)),
        };
        Console.WriteLine($"  ✓ وارد شد: {res.Imported}  ·  از قبل موجود: {res.Skipped}  ·  ناموفق: {res.Failed}");
        foreach (var e in res.Errors) Console.WriteLine("      • " + e);
    }
    catch (Exception ex) { Console.WriteLine("  ✗ خطا: " + ex.GetBaseException().Message); }
}

Console.WriteLine("پایان. (Enter)");
Console.ReadLine();
