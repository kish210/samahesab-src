using SamaHesab.Domain.Entities.Inventory;

namespace SamaHesab.Domain.Interfaces.Repositories;

public interface IStockCountRepository : IRepository<StockCountSession>
{
    /// <summary>سند انبارگردانی به‌همراه ردیف‌هایش.</summary>
    Task<StockCountSession?> GetWithLinesAsync(int id, CancellationToken ct = default);
}
