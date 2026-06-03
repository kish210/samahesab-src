using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Infrastructure.Seed;

/// <summary>Seeds restaurant menu categories + items (idempotent) so restoran.exe has a realistic menu.</summary>
public static class RestaurantSeeder
{
    public static async Task EnsureMenuAsync(IServiceProvider services, int companyId = 1)
    {
        using var scope = services.CreateScope();
        var groups = scope.ServiceProvider.GetRequiredService<IRepository<ProductGroup>>();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        if (await groups.AnyAsync(g => g.CompanyId == companyId)) return; // already seeded

        var unitId = (await products.FindAsync(p => p.CompanyId == companyId)).FirstOrDefault()?.UnitId ?? 1;

        var menu = new (string Code, string Name, (string code, string name, decimal price)[] Items)[]
        {
            ("CAT1","پیش‌غذا", new[]{ ("RST101","سوپ جو",180_000m),("RST102","سالاد سزار",320_000m),("RST103","زیتون پرورده",150_000m) }),
            ("CAT2","غذای اصلی", new[]{ ("RST201","چلوکباب کوبیده",1_250_000m),("RST202","جوجه‌کباب",1_150_000m),("RST203","چلو خورشت قیمه",980_000m) }),
            ("CAT3","دریایی", new[]{ ("RST301","ماهی کامل خلیج فارس",2_400_000m),("RST302","میگو سوخاری",1_850_000m),("RST303","قلیه میگو",2_100_000m) }),
            ("CAT4","نوشیدنی سرد", new[]{ ("RST401","نوشابه",90_000m),("RST402","دوغ",80_000m),("RST403","آب‌معدنی",50_000m) }),
            ("CAT5","نوشیدنی گرم", new[]{ ("RST501","چای",70_000m),("RST502","قهوه",180_000m),("RST503","نسکافه",160_000m) }),
        };

        foreach (var cat in menu)
        {
            var group = ProductGroup.Create(companyId, cat.Code, cat.Name);
            await groups.AddAsync(group);
            await uow.SaveChangesAsync(); // get group.Id

            foreach (var (code, name, price) in cat.Items)
            {
                var p = Product.Create(companyId, code, name, unitId, price, price * 0.6m, ProductType.Service);
                p.UpdateDetails(name, null, group.Id, null, null, null, null);
                await products.AddAsync(p);
            }
            await uow.SaveChangesAsync();
        }
    }
}
