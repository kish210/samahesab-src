using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Hotel.Domain;

/// <summary>ریپازیتوریِ فولیو با Includeِ شارژها/پرداخت‌ها — نگاه کن به توضیحِ IReservationRepository.</summary>
public interface IFolioRepository : IRepository<Folio>
{
    Task<Folio?> GetWithLinesAsync(int id, CancellationToken ct = default);
    Task<Folio?> FindSingleWithLinesAsync(System.Linq.Expressions.Expression<Func<Folio, bool>> predicate, CancellationToken ct = default);
}
