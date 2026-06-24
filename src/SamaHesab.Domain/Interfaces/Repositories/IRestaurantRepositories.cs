namespace SamaHesab.Domain.Interfaces.Repositories;

// IRestaurantOrderRepository → منتقل شد به SamaHesab.Modules.Restaurant.Domain (MOD-REST).

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
