using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.Contracting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۲ — اثباتِ removability برای ماژولِ پیمانکاری (مشابهِ پایلوتِ هتل).</summary>
public class ContractingModuleRemovabilityTests
{
    private static ApplicationDbContext BuildContext(bool withContracting)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True;" })
            .Build();
        var s = new ServiceCollection();
        s.AddInfrastructure(config);
        s.AddSingleton<MediatR.IPublisher>(new StubPub());
        if (withContracting) s.AddSingleton<IModule, ContractingModule>();
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
    public void Core_Works_Without_Contracting_Module()
    {
        using var ctx = BuildContext(withContracting: false);
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.Accounting.Account)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Contracting.Domain.ContractProject)));
    }

    [Fact]
    public void Contracting_Mapped_When_Installed()
    {
        using var ctx = BuildContext(withContracting: true);
        var proj = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Contracting.Domain.ContractProject));
        Assert.NotNull(proj);
        Assert.Equal("Con", proj!.GetSchema());
        Assert.Equal("Projects", proj.GetTableName());
    }
}
