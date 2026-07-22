using Microsoft.EntityFrameworkCore;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Infrastructure.Repositories;
using SamaHesab.Modules.Hotel.Domain;

namespace SamaHesab.Modules.Hotel.Infrastructure;

public class ReservationRepository : GenericRepository<Reservation>, IReservationRepository
{
    public ReservationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Reservation?> GetWithRoomsAsync(int id, CancellationToken ct = default)
        => await DbSet.Include(r => r.Rooms).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<List<Reservation>> FindWithRoomsAsync(
        System.Linq.Expressions.Expression<Func<Reservation, bool>> predicate, CancellationToken ct = default)
        => await DbSet.Include(r => r.Rooms).Where(predicate).ToListAsync(ct);
}
