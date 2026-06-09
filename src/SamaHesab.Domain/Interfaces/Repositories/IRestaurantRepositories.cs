using SamaHesab.Domain.Entities.Restaurant;

namespace SamaHesab.Domain.Interfaces.Repositories;

public interface IRestaurantOrderRepository : IRepository<RestaurantOrder>
{
    /// <summary>سفارش به‌همراه ردیف‌هایش (Items) برای ویرایش aggregate.</summary>
    Task<RestaurantOrder?> GetWithItemsAsync(int id, CancellationToken ct = default);

    Task<int> CountByCompanyAsync(int companyId, CancellationToken ct = default);
}
