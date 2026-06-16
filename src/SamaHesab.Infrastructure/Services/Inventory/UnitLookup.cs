using System.Data;
using Microsoft.EntityFrameworkCore;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Infrastructure.Data;

namespace SamaHesab.Infrastructure.Services.Inventory;

/// <summary>
/// فاز ۱۲ G4.2 — خواندنِ `Cfg.Units` (که entityِ EF ندارد) از طریقِ اتصالِ DbContext.
/// </summary>
public sealed class UnitLookup : IUnitLookup
{
    private readonly ApplicationDbContext _db;
    public UnitLookup(ApplicationDbContext db) => _db = db;

    public IReadOnlyDictionary<string, int> All()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var conn = _db.Database.GetDbConnection();
        var wasClosed = conn.State != ConnectionState.Open;
        try
        {
            if (wasClosed) conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Name FROM Cfg.Units ORDER BY Id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var name = r.IsDBNull(1) ? null : r.GetString(1);
                if (!string.IsNullOrWhiteSpace(name)) map[name.Trim()] = r.GetInt32(0);
            }
        }
        catch { /* graceful: نبودِ جدول → نگاشتِ خالی */ }
        finally { if (wasClosed && conn.State == ConnectionState.Open) conn.Close(); }
        return map;
    }

    public int? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return All().TryGetValue(name.Trim(), out var id) ? id : null;
    }

    public int? DefaultUnitId()
    {
        var all = All();
        if (all.Count == 0) return null;
        return all.TryGetValue("عدد", out var id) ? id : all.Values.Min();
    }
}
