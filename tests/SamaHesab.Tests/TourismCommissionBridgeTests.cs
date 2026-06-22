using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Tourism;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Entities.Tourism;
using DomainBasis = SamaHesab.Domain.Entities.Tourism.CommissionBasis;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C1-6 — پلِ پورسانت→حقوق: جمعِ پورسانتِ ماه per-فروشنده و نگاشتِ Party→Employee با کدِ ملی.</summary>
public class TourismCommissionBridgeTests
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

    private static Party Supplier(FakeRepo<Party> repo, string nationalCode)
    {
        var p = Party.Create(1, "P" + nationalCode, "حقیقی", "فروشنده", nationalCode);
        typeof(Party).GetProperty("NationalCode")!.SetValue(p, nationalCode);
        repo.AddAsync(p).Wait();
        return p;
    }

    [Fact]
    public async Task Bridge_Sums_Commission_Per_Employee_By_NationalCode()
    {
        var emps = new FakeRepo<Employee>();
        emps.AddAsync(Employee.Create(1, 1, "E1", "999", "علی", "فروشنده", "1404/01/01", 10_000_000m)).Wait(); // Id=1, کدملی 999
        emps.AddAsync(Employee.Create(1, 1, "E2", "888", "رضا", "دیگر", "1404/01/01", 10_000_000m)).Wait();    // Id=2

        var parties = new FakeRepo<Party>();
        var seller = Supplier(parties, "999");   // فروشنده‌ی مرتبط با کارمندِ کدملی ۹۹۹
        var orphan = Supplier(parties, "777");   // فروشنده‌ی بدونِ کارمندِ متناظر

        var comm = new FakeRepo<SalesCommissionEntry>();
        comm.AddAsync(SalesCommissionEntry.Create(1, 1, seller.Id, DomainBasis.PercentOfSale, 1000, 5, 1000, "140406")).Wait();
        comm.AddAsync(SalesCommissionEntry.Create(1, 2, seller.Id, DomainBasis.PercentOfSale, 500, 5, 500, "140406")).Wait();
        comm.AddAsync(SalesCommissionEntry.Create(1, 3, orphan.Id, DomainBasis.PercentOfSale, 200, 5, 200, "140406")).Wait();
        comm.AddAsync(SalesCommissionEntry.Create(1, 4, seller.Id, DomainBasis.PercentOfSale, 999, 5, 999, "140405")).Wait(); // ماهِ دیگر

        var byEmp = await CommissionPayrollBridge.ByEmployeeAsync(comm, parties, emps.Items, 1, "140406", default);

        Assert.Single(byEmp);                       // فقط فروشنده‌ی دارای کارمند
        Assert.Equal(1500m, byEmp[1]);              // ۱۰۰۰+۵۰۰ همان ماه؛ ماهِ دیگر و یتیم حذف
    }
}
