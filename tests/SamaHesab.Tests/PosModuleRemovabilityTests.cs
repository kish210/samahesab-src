using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.POS;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>اثباتِ removability برای ماژولِ POS (هم‌الگوی Hotel/Contracting).</summary>
public class PosModuleRemovabilityTests
{
    private static ApplicationDbContext BuildContext(bool withPos)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True;" })
            .Build();
        var s = new ServiceCollection();
        s.AddInfrastructure(config);
        s.AddSingleton<MediatR.IPublisher>(new StubPub());
        if (withPos) s.AddSingleton<IModule, PosModule>();
        return s.BuildServiceProvider().GetRequiredService<ApplicationDbContext>();
    }

    private sealed class StubPub : MediatR.IPublisher
    {
        public System.Threading.Tasks.Task Publish(object n, System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task Publish<T>(T n, System.Threading.CancellationToken ct = default)
            where T : MediatR.INotification => System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact]
    public void Core_Works_Without_Pos_Module()
    {
        using var ctx = BuildContext(withPos: false);
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.Accounting.Account)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.POS.Domain.CashShift)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.POS.Domain.HeldSale)));
    }

    [Fact]
    public void Pos_Mapped_When_Installed()
    {
        using var ctx = BuildContext(withPos: true);
        var shift = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.POS.Domain.CashShift));
        Assert.NotNull(shift);
        Assert.Equal("Pos", shift!.GetSchema());
        Assert.Equal("CashShifts", shift.GetTableName());
    }
}
