using System.Text;
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
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

// Ensure a default admin + restaurant menu exist (idempotent) so the API is usable on a fresh DB.
try { await SamaHesab.Infrastructure.Identity.IdentitySeeder.EnsureDefaultAdminAsync(app.Services); }
catch (Exception ex) { app.Logger.LogWarning(ex, "Identity seeding skipped (DB unavailable?)"); }
try { await SamaHesab.Infrastructure.Seed.RestaurantSeeder.EnsureMenuAsync(app.Services); }
catch (Exception ex) { app.Logger.LogWarning(ex, "Restaurant menu seeding skipped"); }

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SamaHesab ERP API v1"));
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger"));

app.Run();
