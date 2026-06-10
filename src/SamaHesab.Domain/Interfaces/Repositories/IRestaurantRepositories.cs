using SamaHesab.Domain.Entities.Restaurant;

namespace SamaHesab.Domain.Interfaces.Repositories;

public interface IRestaurantOrderRepository : IRepository<RestaurantOrder>
{
    /// <summary>سفارش به‌همراه ردیف‌هایش (Items) برای ویرایش aggregate.</summary>
    Task<RestaurantOrder?> GetWithItemsAsync(int id, CancellationToken ct = default);

    Task<int> CountByCompanyAsync(int companyId, CancellationToken ct = default);
}

public interface IVoucherTemplateRepository : IRepository<Entities.Accounting.VoucherTemplate>
{
    /// <summary>الگو به‌همراه ردیف‌هایش (برای ساخت سند از روی الگو).</summary>
    Task<Entities.Accounting.VoucherTemplate?> GetWithLinesAsync(int id, CancellationToken ct = default);

    Task<List<Entities.Accounting.VoucherTemplate>> GetByCompanyAsync(int companyId, CancellationToken ct = default);
}

public interface IRecurringVoucherRepository : IRepository<Entities.Accounting.RecurringVoucher>
{
    Task<List<Entities.Accounting.RecurringVoucher>> GetActiveAsync(int companyId, CancellationToken ct = default);
}
