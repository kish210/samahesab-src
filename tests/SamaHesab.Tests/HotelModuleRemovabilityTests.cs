using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Infrastructure;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Modules.Abstractions;
using SamaHesab.Modules.Hotel;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// فاز ۱ — اثباتِ removability برای پایلوتِ هتل: هسته نباید موجودیتِ ماژول را بشناسد.
/// بدونِ ثبتِ HotelModule → جدول‌های Htl در مدل نیستند ولی موجودیت‌های هسته سالم‌اند (هسته نمی‌شکند).
/// با ثبتِ HotelModule → موجودیت‌های هتل مپ می‌شوند (نصب‌شده).
/// مدل بدونِ اتصالِ DB ساخته می‌شود (OnModelCreating به دیتابیس وصل نمی‌شود).
/// </summary>
public class HotelModuleRemovabilityTests
{
    private static ApplicationDbContext BuildContext(bool withHotel)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=x;Trusted_Connection=True;TrustServerCertificate=True;" })
            .Build();
        var s = new ServiceCollection();
        s.AddInfrastructure(config);
        s.AddSingleton<MediatR.IPublisher>(new StubPublisher());   // DbContext به IPublisher نیاز دارد (میزبان معمولاً MediatR را ثبت می‌کند)
        if (withHotel) s.AddSingleton<IModule, HotelModule>();
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
    public void Core_Works_Without_Hotel_Module_And_Hotel_Is_Not_Mapped()
    {
        using var ctx = BuildContext(withHotel: false);
        // هسته سالم: موجودیتِ هسته‌ای مپ است.
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Domain.Entities.Accounting.Account)));
        // ماژولِ نصب‌نشده: موجودیتِ هتل اصلاً در مدل نیست (هسته آن را نمی‌شناسد).
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Hotel.Domain.RoomType)));
        Assert.Null(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Hotel.Domain.Reservation)));
    }

    [Fact]
    public void Hotel_Is_Mapped_When_Module_Installed()
    {
        using var ctx = BuildContext(withHotel: true);
        var room = ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Hotel.Domain.RoomType));
        Assert.NotNull(room);
        Assert.Equal("Htl", room!.GetSchema());          // schemaی اختصاصیِ ماژول
        Assert.Equal("RoomTypes", room.GetTableName());
        Assert.NotNull(ctx.Model.FindEntityType(typeof(SamaHesab.Modules.Hotel.Domain.Reservation)));
        // یادداشت: چون ConfigureModelِ ماژول *پیش از* حلقهٔ فیلترِ عمومی اجرا می‌شود و موجودیت‌های هتل
        // AuditableEntity‌اند، فیلترِ سراسریِ شرکت (multi-tenant) خودکار رویشان اعمال می‌شود.
    }
}
