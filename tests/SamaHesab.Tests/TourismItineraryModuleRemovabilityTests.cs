using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.TourismItinerary;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// removability برای ماژولِ TourismItinerary: بدونِ ثبتِ ماژول → موجودیت‌هایش در مدل نیستند ولی
/// هسته سالم است؛ با ثبتِ ماژول → موجودیت‌ها در schemaی اختصاصیِ Tit مپ می‌شوند.
/// </summary>
public class TourismItineraryModuleRemovabilityTests
{
    private static ApplicationDbContext BuildContext(bool withModule)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True;" })
            .Build();
        var s = new ServiceCollection();
        s.AddInfrastructure(config);
        s.AddSingleton<MediatR.IPublisher>(new StubPublisher());
        if (withModule) s.AddSingleton<IModule, TourismItineraryModule>();
        return s.BuildServiceProvider().GetRequiredService<ApplicationDbContext>();
    }

    private sealed class StubPublisher : MediatR.IPublisher
    {
        public System.Threading.Tasks.Task Publish(object notification, System.Threading.CancellationToken ct = default)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task Publish<TNotification>(TNotification notification, System.Threading.CancellationToken ct = default)
            where TNotification : MediatR.INotification => System.Threading.Tasks.Task.CompletedTask;
    }

    [Fact]
    public void Core_Works_Without_Module_And_Itinerary_Is_Not_Mapped()
    {
        using var ctx = BuildContext(withModule: false);
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.Accounting.Account)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TourismItinerary.Domain.ItineraryProduct)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TourismItinerary.Domain.GuestItinerary)));
    }

    [Fact]
    public void Module_Entities_Are_Mapped_In_Tit_Schema_When_Installed()
    {
        using var ctx = BuildContext(withModule: true);
        var product = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TourismItinerary.Domain.ItineraryProduct));
        Assert.NotNull(product);
        Assert.Equal("Tit", product!.GetSchema());
        Assert.Equal("Products", product.GetTableName());

        var itinerary = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TourismItinerary.Domain.GuestItinerary));
        Assert.NotNull(itinerary);
        Assert.Equal("Tit", itinerary!.GetSchema());
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.TourismItinerary.Domain.ItineraryStop)));
    }
}
