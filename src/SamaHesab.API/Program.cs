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
})
{
    builder.Services.AddSingleton<SamaHesab.Modules.Abstractions.IModule>(module);
    module.RegisterServices(builder.Services);
}

// همگام‌سازیِ پشتیبانی سمتِ کلاینت است؛ میزبانِ API نسخهٔ no-op می‌گیرد (رفعِ ValidateOnBuild در Development).
builder.Services.AddSingleton<SamaHesab.Application.Support.ISupportApiClient, SamaHesab.API.Services.OfflineSupportApiClient>();

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

app.Run();
