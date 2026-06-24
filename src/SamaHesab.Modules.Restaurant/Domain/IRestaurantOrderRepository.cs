using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Restaurant.Domain;

namespace SamaHesab.Modules.Restaurant.Domain;

/// <summary>ریپازیتوریِ سفارشِ رستوران (منتقل‌شده از هسته به ماژول، MOD-REST).</summary>
public interface IRestaurantOrderRepository : IRepository<RestaurantOrder>
{
    /// <summary>سفارش به‌همراه ردیف‌هایش (Items) برای ویرایش aggregate.</summary>
    Task<RestaurantOrder?> GetWithItemsAsync(int id, CancellationToken ct = default);

    Task<int> CountByCompanyAsync(int companyId, CancellationToken ct = default);
}
