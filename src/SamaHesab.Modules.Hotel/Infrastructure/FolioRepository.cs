using Microsoft.EntityFrameworkCore;
using SamaHesab.Infrastructure.Data;
using SamaHesab.Infrastructure.Repositories;
using SamaHesab.Modules.Hotel.Domain;

namespace SamaHesab.Modules.Hotel.Infrastructure;

public class FolioRepository : GenericRepository<Folio>, IFolioRepository
{
    public FolioRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Folio?> GetWithLinesAsync(int id, CancellationToken ct = default)
        => await DbSet.Include(f => f.Charges).Include(f => f.Payments).FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<Folio?> FindSingleWithLinesAsync(
        System.Linq.Expressions.Expression<Func<Folio, bool>> predicate, CancellationToken ct = default)
        => await DbSet.Include(f => f.Charges).Include(f => f.Payments).FirstOrDefaultAsync(predicate, ct);
}
