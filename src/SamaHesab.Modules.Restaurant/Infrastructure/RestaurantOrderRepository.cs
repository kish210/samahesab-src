using Microsoft.EntityFrameworkCore;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Infrastructure.Repositories;
using SamaHesab.Modules.Restaurant.Domain;

namespace SamaHesab.Modules.Restaurant.Infrastructure;

/// <summary>ریپازیتوریِ سفارشِ رستوران (منتقل‌شده از هسته، MOD-REST). روی ApplicationDbContextِ هسته کار می‌کند.</summary>
public class RestaurantOrderRepository
    : GenericRepository<RestaurantOrder>, IRestaurantOrderRepository
{
    public RestaurantOrderRepository(ApplicationDbContext context) : base(context) { }

    public async Task<RestaurantOrder?> GetWithItemsAsync(int id, CancellationToken ct = default)
        => await DbSet.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<int> CountByCompanyAsync(int companyId, CancellationToken ct = default)
        => await DbSet.CountAsync(o => o.CompanyId == companyId, ct);
}
