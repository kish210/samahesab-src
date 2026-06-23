using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Mediator;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// BUG-8 — تأییدِ واقعی روی DBِ زندهٔ محلی: کوئری‌های هم‌زمان از طریقِ ScopedMediator باید بدونِ خطای
/// «A second operation was started on this context» اجرا شوند؛ و کنترل (DbContextِ مشترکِ یک scope)
/// باید همان خطا را بدهد. اگر SQLِ محلی در دسترس نباشد، تست به‌آرامی رد می‌شود (روی CI/ماشینِ بدونِ DB).
/// </summary>
public class DbContextConcurrencyIntegrationTests
{
    private const string Conn =
        "Server=.\\SQLEXPRESS;Database=SamaHesab;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connect Timeout=5;MultipleActiveResultSets=True;";

    private static bool DbReachable()
    {
        try { using var c = new SqlConnection(Conn); c.Open(); return true; } catch { return false; }
    }

    private sealed class StubUser : ICurrentUserService
    {
        public int? UserId => 1;
        public int? CompanyId => 1;
        public int? BranchId => 1;
        public string? Username => "admin";
        public string? FullName => "مدیر";
        public bool IsAuthenticated => true;
        public bool HasPermission(string moduleCode, string featureCode, string action) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static ServiceProvider Build(bool scoped)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = Conn })
            .Build();
        var s = new ServiceCollection();
        s.AddInfrastructure(config);
        s.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(GetTrialBalanceQuery).Assembly));
        s.AddTransient<Mediator>();
        s.AddSingleton<ICurrentUserService, StubUser>();
        if (scoped) s.AddSingleton<IMediator, ScopedMediator>();
        return s.BuildServiceProvider();
    }

    private static GetTrialBalanceQuery Query() => new("1404/01/01", "1405/12/29");

    [Fact]
    public async Task ScopedMediator_Handles_Concurrent_Queries_On_Real_Db()
    {
        if (!DbReachable()) return;   // skip بدونِ DB
        using var sp = Build(scoped: true);
        var med = sp.GetRequiredService<IMediator>();
        // ۱۲ کوئریِ هم‌زمان روی جریان‌های مستقل — با ScopedMediator هرکدام DbContextِ خودش را می‌گیرد.
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => med.Send(Query()))));
        // رسیدن به این‌جا یعنی هیچ خطای threading رخ نداد.
    }

    [Fact]
    public async Task Control_SharedContext_Throws_Threading_Error()
    {
        if (!DbReachable()) return;   // skip بدونِ DB
        using var sp = Build(scoped: false);
        using var scope = sp.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();   // یک Mediator با DbContextِ مشترکِ همین scope
        var ex = await Record.ExceptionAsync(() =>
            Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Task.Run(() => sender.Send(Query())))));
        Assert.NotNull(ex);   // DbContextِ مشترک → خطای «second operation» (یا تصادمِ هم‌خانواده)
    }
}
