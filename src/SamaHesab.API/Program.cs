using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SamaHesab.API.Services;
using SamaHesab.Application;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// فاز ۱۲ P-G8 — اجرا به‌عنوانِ Windows Service (وقتی توسطِ SCM اجرا شود) تا سرور بدونِ
// کنسولِ دستی، خودکار و در پس‌زمینه بالا بماند. در محیطِ توسعه/کنسول بی‌اثر است.
builder.Host.UseWindowsService(o => o.ServiceName = "SamaHesabApi");

// ── Reuse the SAME Application + Infrastructure layers as the desktop client ──
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── ماژول‌های نصب‌شده (ماژولارسازی فاز ۱/۲): هتل + پیمانکاری. هر ماژول IModule ثبت می‌شود
//    (ApplicationDbContext برای مپِ مدل) و RegisterServicesش (هندلرهای MediatR) صدا زده می‌شود. ──
foreach (var module in new SamaHesab.Modules.Abstractions.IModule[]
{
    new SamaHesab.Modules.Hotel.HotelModule(),
    new SamaHesab.Modules.Contracting.ContractingModule(),
    new SamaHesab.Modules.CRM.CrmModule(),   // فاز ۳ (pc) — باشگاه/امتیاز
    new SamaHesab.Modules.Restaurant.RestaurantModule(),   // MOD-REST (laptop)
    new SamaHesab.Modules.HR.HrModule(),     // فاز ۳ (pc) — حقوق
    new SamaHesab.Modules.POS.PosModule(),                 // MOD-POS (laptop)
    new SamaHesab.Modules.Tourism.TourismModule(),         // MOD-TUR (laptop)
    new SamaHesab.Modules.Attendance.AttendanceModule(),   // فاز ۳ (pc) — حضوروغیاب
    new SamaHesab.Modules.TaxInvoicing.TaxInvoicingModule(), // سامانهٔ مودیان (laptop)
})
{
    builder.Services.AddSingleton<SamaHesab.Modules.Abstractions.IModule>(module);
    module.RegisterServices(builder.Services);
}

// ── U-MODULE-INSTALL — ماژول‌هایِ نصب‌شده از فایل (فولدرِ سرور، بدونِ گیت/rebuild) ──
//    مسیر: %ProgramData%\SamaHesab\modules\*.mspkg (یا Modules:Directory). هر ماژولِ کشف‌شده که
//    کلیدش قبلاً bundle نشده باشد، مثلِ ماژول‌هایِ بالا ثبت و RegisterServicesش صدا زده می‌شود.
//    (بارگذاری در startup است ⇒ ماژولِ تازه‌آپلودشده با ری‌استارتِ سرور فعال می‌شود.)
try
{
    var bundledKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var m in builder.Services
        .Where(s => s.ServiceType == typeof(SamaHesab.Modules.Abstractions.IModule) && s.ImplementationInstance is SamaHesab.Modules.Abstractions.IModule)
        .Select(s => (SamaHesab.Modules.Abstractions.IModule)s.ImplementationInstance!))
        bundledKeys.Add(m.Key);

    var modulesDir = SamaHesab.Infrastructure.Modules.ModuleLoader.ServerModulesDirectory(
        builder.Configuration["Modules:Directory"]);
    var installed = SamaHesab.Infrastructure.Modules.ModuleLoader.LoadFromDirectory(
        modulesDir, bundledKeys, msg => Console.WriteLine(msg));
    foreach (var module in installed)
    {
        builder.Services.AddSingleton(module);
        module.RegisterServices(builder.Services);
    }
}
catch (Exception ex) { Console.WriteLine($"[module] server folder load skipped: {ex.Message}"); }

// U-WEB-SUPPORT — کانفیگ از appsettings.json (بخشِ "Support") در kishwifi.com می‌خواند؛ اگر
// تنظیم نشده باشد، دقیقاً رفتارِ OfflineSupportApiClientِ قبلی را دارد (fail-soft، نه throw).
builder.Services.AddSingleton<SamaHesab.Application.Support.ISupportApiClient, SamaHesab.API.Services.ConfiguredSupportApiClient>();

// ── API-side current-user (from JWT claims) ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<RefreshTokenStore>();

// زمان‌بند پس‌زمینه‌ی اعلان‌ها (P2، کار #۷)
builder.Services.AddHostedService<SamaHesab.API.Services.NotificationSchedulerService>();
// زمان‌بند تولید اسناد/فاکتورهای تکرارشونده (P3، کار #۱۱)
builder.Services.AddHostedService<SamaHesab.API.Services.RecurringDocumentSchedulerService>();

// ── JWT authentication ──
var jwt = builder.Configuration.GetSection("Jwt");
var jwtKey = jwt["Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException(
        "تنظیمِ Jwt:Key در appsettings.json موجود نیست — برای امضای توکنِ JWT الزامی است.");
// امنیتِ فروش (SR-3/SR-4): کلیدِ پیش‌فرضِ درونِ ریپو یا کلیدِ کوتاه، توکن‌ها را جعل‌پذیر می‌کند.
// در Productionِ واقعی (نه Development) اگر appsettings.json هنوز کلیدِ نمونه/کوتاه دارد، به‌جایِ
// شکستِ استارت‌آپ، یک کلیدِ تصادفیِ ۶۴بایتی **یک‌بار** تولید و در پوشهٔ محلیِ ماشین (نه در ریپو/appsettings)
// نگه‌داری می‌شود؛ اجراهای بعدی همان کلید را دوباره می‌خوانند (توکن‌های صادرشده معتبر می‌مانند).
if (!builder.Environment.IsDevelopment()
    && (jwtKey.StartsWith("CHANGE_THIS", StringComparison.OrdinalIgnoreCase)
        || System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32))
{
    var keyDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SamaHesab");
    var keyFile = Path.Combine(keyDir, "jwt.key");
    if (File.Exists(keyFile))
    {
        jwtKey = File.ReadAllText(keyFile).Trim();
    }
    else
    {
        Directory.CreateDirectory(keyDir);
        var randomKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
        File.WriteAllText(keyFile, randomKey);
        jwtKey = randomKey;
    }
    if (System.Text.Encoding.UTF8.GetByteCount(jwtKey) < 32)
        throw new InvalidOperationException(
            $"کلیدِ ذخیره‌شده در «{keyFile}» نامعتبر است؛ آن را حذف کنید تا دوباره تولید شود.");
}
// SR-3/SR-4 fix: JwtTokenService باید همین کلیدِ مؤثر (پس از override) را برایِ امضا استفاده کند،
// نه کلیدِ خامِ appsettings را دوباره بخواند — وگرنه در Productionِ بدونِ کلیدِ واقعی، توکنِ صادرشده
// با کلیدی متفاوت از کلیدِ اعتبارسنجیِ زیر امضا می‌شود و هر لاگین با ۴۰۱ رد می‌شود.
var accessMinutes = int.TryParse(jwt["AccessTokenMinutes"], out var jwtMinutes) ? jwtMinutes : 60;
builder.Services.AddSingleton(new JwtSettings(jwtKey, jwt["Issuer"], jwt["Audience"], accessMinutes));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();   // پاسخِ خطای یکدستِ JSON (RFC 7807)

// ── نسخه‌بندیِ API (P7): پیش‌فرض v1؛ نسخه از هدرِ X-Api-Version یا کوئریِ api-version.
//    AssumeDefaultVersionWhenUnspecified → مسیرهای موجود و کلاینتِ فعلی بدونِ تغییر کار می‌کنند (v1).
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ReportApiVersions = true;   // هدرِ api-supported-versions در پاسخ
    o.ApiVersionReader = ApiVersionReader.Combine(
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version"));
}).AddApiExplorer(o =>
{
    o.GroupNameFormat = "'v'VVV";       // v1، v2، …
    o.SubstituteApiVersionInUrl = false; // مسیرها URL-segment ندارند (سازگار با کلاینتِ فعلی)
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// ── Swagger with Bearer support ──
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SamaHesab ERP API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
});

var app = builder.Build();

// نصبِ تازهٔ سرور: ساختِ خودکارِ پایگاه‌داده + اسکیمای پایه (مثلِ کلاینتِ دسکتاپ) — پیش از seed،
// تا API روی سرورِ بدونِ DB هم خودکار بالا بیاید (هم‌سان با رفعِ نصابِ دسکتاپ).
try
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(cs))
        await SamaHesab.Infrastructure.Data.DatabaseMigrator.RunAsync(cs, m => app.Logger.LogInformation("[DB] {Msg}", m));
}
catch (Exception ex) { app.Logger.LogWarning(ex, "بوت‌استرپِ پایگاه‌داده رد شد"); }

// Ensure a default admin + restaurant menu exist (idempotent) so the API is usable on a fresh DB.
try { await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(app.Services); }
catch (Exception ex) { app.Logger.LogWarning(ex, "Identity seeding skipped (DB unavailable?)"); }
try { await SamaHesab.Infrastructure.Seed.RestaurantSeeder.EnsureMenuAsync(app.Services); }
catch (Exception ex) { app.Logger.LogWarning(ex, "Restaurant menu seeding skipped"); }

// خطاهای ناهندل‌شده → پاسخِ ProblemDetailsِ یکدست (به‌جای ۵۰۰ خام).
app.UseExceptionHandler();

// PWAِ موبایل (MOBILE-1): فایل‌های wwwroot/app/* را سرو می‌کند → کلاینتِ موبایل در /app/.
// SP-3b: پنلِ فروشِ Blazor در wwwroot/seller/* روی /seller/ سرو می‌شود.
// هم‌مبدأ با API است، پس نیازی به CORS ندارد و JWT مستقیم کار می‌کند.
// نکته: نوع‌های فایلِ runtimeِ Blazor (icudt*.dat / .blat / .dll) را ASP.NET به‌طورِ پیش‌فرض
// سرو نمی‌کند (۴۰۴ → «Failed to start platform»). با ContentTypeProvider اضافه‌شان می‌کنیم.
var blazorCtp = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
blazorCtp.Mappings[".dat"]  = "application/octet-stream";   // دادهٔ ICU گلوبالیزیشن
blazorCtp.Mappings[".blat"] = "application/octet-stream";
blazorCtp.Mappings[".dll"]  = "application/octet-stream";
blazorCtp.Mappings[".wasm"] = "application/wasm";

// ── پنلِ مهمانِ برنامه‌ریزیِ اقامتی روی پورتِ اختصاصی (۵۰۹۰)، جدا از پورتِ API (۵۰۸۰) ──
//    سرور روی هر دو پورت گوش می‌دهد. روی ۵۰۹۰: اپِ Blazorِ مهمان (base href=/) از wwwroot/guest با
//    SPA-fallback سرو می‌شود تا لینکِ عمیقِ /itinerary/{token} مستقیم باز شود. درخواست‌های /api روی
//    ۵۰۹۰ به همان کنترلرها می‌روند (همان‌مبدأ، بی‌نیاز از CORS).
const int guestPortalPort = 5090;
var guestDir = System.IO.Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "guest");
if (System.IO.Directory.Exists(guestDir))
{
    var guestFiles = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(guestDir);
    app.MapWhen(
        ctx => ctx.Connection.LocalPort == guestPortalPort && !ctx.Request.Path.StartsWithSegments("/api"),
        branch =>
        {
            branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = guestFiles });
            branch.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions { FileProvider = guestFiles, ContentTypeProvider = blazorCtp });
            branch.Run(async ctx =>   // هر مسیرِ غیرفایلیِ Blazor (مثلِ /itinerary/{token}) → index.html
            {
                ctx.Response.ContentType = "text/html";
                await ctx.Response.SendFileAsync(guestFiles.GetFileInfo("index.html"));
            });
        });
}

// ── کلاینتِ وبِ React (فازِ ۶) روی /web/ ──────────────────────────────────────────
//    فایل‌هایِ buildِ Vite از wwwroot/web سرو می‌شوند. app.Map پیشوندِ /web را از مسیر حذف
//    می‌کند، پس /web/assets/x.js → assets/x.js از همین پوشه خوانده می‌شود. مسیرهایِ غیرفایلیِ
//    مثلِ /web/customers/4 (routingِ سمتِ کلاینت) باید به index.html برگردند وگرنه رفرش/لینکِ
//    مستقیم ۴۰۴ می‌دهد — هم‌الگو با SPA-fallbackِ پنلِ مهمان بالا.
var webClientDir = System.IO.Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "web");
if (System.IO.Directory.Exists(webClientDir))
{
    var webClientFiles = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webClientDir);
    app.Map("/web", branch =>
    {
        branch.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webClientFiles });
        branch.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions { FileProvider = webClientFiles });
        branch.Run(async ctx =>
        {
            ctx.Response.ContentType = "text/html";
            await ctx.Response.SendFileAsync(webClientFiles.GetFileInfo("index.html"));
        });
    });
}

app.UseDefaultFiles();
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions { ContentTypeProvider = blazorCtp });

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SamaHesab ERP API v1"));
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

// سلامت‌سنجی (برای مانیتورینگ/Windows Service) — اتصالِ DB را هم چک می‌کند. بدونِ احراز.
app.MapGet("/health", async (SamaHesab.Infrastructure.Data.ApplicationDbContext db) =>
{
    bool dbOk;
    try { dbOk = await db.Database.CanConnectAsync(); } catch { dbOk = false; }
    return Results.Ok(new { status = dbOk ? "healthy" : "degraded", db = dbOk, utc = DateTime.UtcNow });
});

// نسخهٔ درحالِ‌اجرایِ API (از AssemblyVersion، خودش از Directory.Build.props مشتق می‌شود) —
// وبِ کلاینت این را برایِ نمایشِ نسخهٔ واقعیِ سرور می‌خواند (به‌جایِ رشتهٔ ثابتِ قدیمی). بدونِ احراز.
app.MapGet("/api/version", () =>
{
    var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
    return Results.Ok(new { version = $"{v.Major}.{v.Minor}.{v.Build}" });
});

app.Run();
