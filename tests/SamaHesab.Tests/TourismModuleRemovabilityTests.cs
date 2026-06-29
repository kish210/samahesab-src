using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.Tourism;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>اثباتِ removability برای ماژولِ Tourism (هم‌الگوی Hotel/Contracting).</summary>
public class TourismModuleRemovabilityTests
{
    private static ApplicationDbContext BuildContext(bool withTourism)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True;" })
            .Build();
        var s = new ServiceCollection();
        s.AddInfrastructure(config);
        s.AddSingleton<MediatR.IPublisher>(new StubPub());
        if (withTourism) s.AddSingleton<IModule, TourismModule>();
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
    public void Core_Works_Without_Tourism_Module()
    {
        using var ctx = BuildContext(withTourism: false);
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.Accounting.Account)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Tourism.Domain.TourismSale)));
        // برنامه‌ریزیِ اقامتی هم زیرمجموعهٔ گردشگری است → بدونِ ماژول مپ نمی‌شود.
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Tourism.Domain.GuestItinerary)));
    }

    [Fact]
    public void Tourism_Mapped_When_Installed()
    {
        using var ctx = BuildContext(withTourism: true);
        var sale = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Tourism.Domain.TourismSale));
        Assert.NotNull(sale);
        Assert.Equal("Tur", sale!.GetSchema());
    }

    [Fact]
    public void Itinerary_Planning_Mapped_Under_Tourism_Schema()
    {
        using var ctx = BuildContext(withTourism: true);
        // برنامه‌ریزیِ اقامتی بخشی از گردشگری است: موجودیت‌هایش در همان schema Tur با نام‌جدول‌های Itinerary*.
        var product = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Tourism.Domain.ItineraryProduct));
        Assert.NotNull(product);
        Assert.Equal("Tur", product!.GetSchema());
        Assert.Equal("ItineraryProducts", product.GetTableName());

        var itinerary = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Tourism.Domain.GuestItinerary));
        Assert.NotNull(itinerary);
        Assert.Equal("Tur", itinerary!.GetSchema());
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Tourism.Domain.ItineraryStop)));
    }
}
