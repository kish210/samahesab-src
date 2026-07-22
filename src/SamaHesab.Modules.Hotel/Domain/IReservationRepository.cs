using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Hotel.Domain;

/// <summary>ریپازیتوریِ رزرو با Includeِ خطوطِ اتاق (Rooms) — IRepository&lt;T&gt;.GetByIdAsync عمومی
/// از DbSet.FindAsync استفاده می‌کند که ناوبری‌ها را بارگذاری نمی‌کند (باگِ کشف‌شده در تستِ زنده:
/// Rooms همیشه خالی می‌ماند ⇒ CheckInCommand هیچ اتاقی تخصیص نمی‌داد).</summary>
public interface IReservationRepository : IRepository<Reservation>
{
    Task<Reservation?> GetWithRoomsAsync(int id, CancellationToken ct = default);
    Task<List<Reservation>> FindWithRoomsAsync(System.Linq.Expressions.Expression<Func<Reservation, bool>> predicate, CancellationToken ct = default);
}
