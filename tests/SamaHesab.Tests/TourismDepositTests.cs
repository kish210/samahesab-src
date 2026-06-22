using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Tourism.Commands;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Tourism;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C1-4 — ماندهٔ ودیعهٔ تأمین‌کننده: شارژ↑ / برداشتِ فروش↓ / آلارمِ ودیعهٔ کم.</summary>
public class TourismDepositTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default)
        { foreach (var e in es) { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> es) { foreach (var x in es.ToList()) Items.Remove(x); }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    [Fact]
    public async Task Balance_Is_TopUps_Minus_Drawdown_And_Flags_Low()
    {
        var settings = new FakeRepo<TourismSetting>();
        var set = TourismSetting.Create(1);
        set.Update(null, null, null, null, null, null, null, null, null, null, true,
            lowDepositThreshold: 700, postPerSale: true, commissionThroughPayroll: true);
        settings.AddAsync(set).Wait();

        var deposits = new FakeRepo<SupplierDeposit>();
        deposits.AddAsync(SupplierDeposit.Create(1, supplierPartyId: 11, amount: 1000, date: "1404/06/01")).Wait();
        deposits.AddAsync(SupplierDeposit.Create(1, supplierPartyId: 12, amount: 5000, date: "1404/06/01")).Wait();

        var lines = new FakeRepo<TourismSaleLine>();
        // تأمین‌کننده ۱۱: برداشتِ ۲×۱۵۰بها + ۱×۱۰۰بها = ۴۰۰ → مانده ۶۰۰ (<۷۰۰ ⇒ کم)
        lines.AddAsync(TourismSaleLine.Create(1, 11, quantity: 2, unitSalePrice: 200, discountAmount: 0, unitCost: 150)).Wait();
        lines.AddAsync(TourismSaleLine.Create(2, 11, quantity: 1, unitSalePrice: 150, discountAmount: 0, unitCost: 100)).Wait();
        // تأمین‌کننده ۱۲: برداشتِ ۱×۳۰۰ → مانده ۴۷۰۰ (>۷۰۰)
        lines.AddAsync(TourismSaleLine.Create(3, 12, quantity: 1, unitSalePrice: 500, discountAmount: 0, unitCost: 300)).Wait();

        var handler = new GetSupplierDepositBalancesQueryHandler(settings, deposits, lines,
            new FakeRepo<Party>(), new FakeUser());

        var all = await handler.Handle(new GetSupplierDepositBalancesQuery(), default);

        var s11 = all.Single(x => x.SupplierPartyId == 11);
        Assert.Equal(1000m, s11.ToppedUp);
        Assert.Equal(400m, s11.Drawn);
        Assert.Equal(600m, s11.Remaining);
        Assert.True(s11.IsLow);                       // ۶۰۰ < ۷۰۰

        var s12 = all.Single(x => x.SupplierPartyId == 12);
        Assert.Equal(4700m, s12.Remaining);
        Assert.False(s12.IsLow);

        var lowOnly = await handler.Handle(new GetSupplierDepositBalancesQuery(OnlyLow: true), default);
        Assert.Single(lowOnly);
        Assert.Equal(11, lowOnly[0].SupplierPartyId);
    }
}
