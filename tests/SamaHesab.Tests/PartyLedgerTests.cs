using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.CRM;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-PARTY-LEDGER (backlog #9) — Party.Balance حالا کشِ Σ(PartyLedgerEntry.Amount) است.
/// این تست‌ها تضمین می‌کنند: (۱) هر RecordAsync هم Balance را درست به‌روز می‌کند هم ردیفِ لجرِ
/// متناظر می‌سازد، (۲) بعد از چند رویدادِ متوالی، Balance دقیقاً با Σ(لجر) آشتی می‌شود (رکانسیلیشن).</summary>
public class PartyLedgerTests
{
    private sealed class FakeLedgerRepo : IRepository<PartyLedgerEntry>
    {
        public readonly List<PartyLedgerEntry> Items = new();
        private int _seq;
        public Task AddAsync(PartyLedgerEntry e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<PartyLedgerEntry> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<PartyLedgerEntry?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id));
        public Task<List<PartyLedgerEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<PartyLedgerEntry>> FindAsync(Expression<System.Func<PartyLedgerEntry, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<PartyLedgerEntry?> FindSingleAsync(Expression<System.Func<PartyLedgerEntry, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<PartyLedgerEntry, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<PartyLedgerEntry, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(PartyLedgerEntry e) { }
        public void Remove(PartyLedgerEntry e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<PartyLedgerEntry> es) { }
    }

    private static Party MakeParty(int id)
    {
        var p = Party.Create(1, "C" + id, "حقیقی", "مشتری", "آزمایشی", isCustomer: true);
        typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(p, id);
        return p;
    }

    [Fact]
    public async Task RecordAsync_Updates_Balance_And_Appends_Ledger_Row()
    {
        var ledger = new FakeLedgerRepo();
        var party = MakeParty(1);

        await PartyLedger.RecordAsync(ledger, party, 500_000, "1405/04/15", "فاکتور فروش", "S1", "فاکتور فروش S1", default);

        Assert.Equal(500_000, party.Balance);
        Assert.Single(ledger.Items);
        Assert.Equal(500_000, ledger.Items[0].Amount);
        Assert.Equal("فاکتور فروش", ledger.Items[0].DocType);
        Assert.Equal("S1", ledger.Items[0].DocNumber);
    }

    [Fact]
    public async Task RecordAsync_With_Zero_Delta_Is_NoOp()
    {
        var ledger = new FakeLedgerRepo();
        var party = MakeParty(1);

        await PartyLedger.RecordAsync(ledger, party, 0, "1405/04/15", "دریافت", null, null, default);

        Assert.Equal(0, party.Balance);
        Assert.Empty(ledger.Items);
    }

    [Fact]
    public async Task After_Several_Events_Balance_Reconciles_Exactly_With_Ledger_Sum()
    {
        var ledger = new FakeLedgerRepo();
        var party = MakeParty(1);

        // فاکتورِ فروشِ نسیه (+)، دریافتِ جزئی (-)، برگشتِ از فروش (-)، تسویهٔ کنسینمنت (+)
        await PartyLedger.RecordAsync(ledger, party, 1_000_000, "1405/04/01", "فاکتور فروش", "S1", null, default);
        await PartyLedger.RecordAsync(ledger, party, -400_000, "1405/04/05", "دریافت", null, null, default);
        await PartyLedger.RecordAsync(ledger, party, -150_000, "1405/04/10", "برگشت از فروش", "BR1", null, default);
        await PartyLedger.RecordAsync(ledger, party, 250_000, "1405/04/12", "تسویهٔ کنسینمنت", "HV1", null, default);

        var expected = 1_000_000m - 400_000m - 150_000m + 250_000m;
        Assert.Equal(expected, party.Balance);
        Assert.Equal(expected, ledger.Items.Sum(e => e.Amount));   // رکانسیلیشن: Balance == Σ(لجر)

        var handler = new GetPartyLedgerQueryHandler(ledger);
        var rows = await handler.Handle(new GetPartyLedgerQuery(party.Id), default);

        Assert.Equal(4, rows.Count);
        Assert.Equal(expected, rows[^1].RunningBalance);   // ماندهٔ درحالِ‌گردشِ آخرین ردیف = مانده نهایی
        Assert.Equal(1_000_000m, rows[0].RunningBalance);  // ماندهٔ ردیفِ اول = فقط فاکتور
    }
}
