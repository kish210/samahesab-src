using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ATTP-C1-3 — پردازشِ ترددِ خام→روزانه (جفت‌سازیِ اولین=ورود/آخرین=خروج).</summary>
public class RawPunchProcessingTests
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

    private sealed class FakeUow : IUnitOfWork
    {
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    [Fact]
    public async Task Process_Pairs_First_In_Last_Out_And_Marks_Processed()
    {
        var punches = new FakeRepo<RawPunch>();
        // کارمند ۱: سه ضربه ۰۸:۰۲ / ۱۲:۰۰ / ۱۷:۱۰ → ورود ۰۸:۰۲، خروج ۱۷:۱۰.
        punches.AddAsync(RawPunch.Create(1, 1, "1404/01/10", new TimeOnly(12, 0))).Wait();
        punches.AddAsync(RawPunch.Create(1, 1, "1404/01/10", new TimeOnly(8, 2))).Wait();
        punches.AddAsync(RawPunch.Create(1, 1, "1404/01/10", new TimeOnly(17, 10))).Wait();
        // کارمند ۲: یک ضربه ۰۹:۰۰ → فقط ورود.
        punches.AddAsync(RawPunch.Create(1, 2, "1404/01/10", new TimeOnly(9, 0))).Wait();

        var records = new FakeRepo<AttendanceRecord>();
        var res = await new ProcessRawPunchesCommandHandler(punches, records, new FakeUow(), new FakeUser())
            .Handle(new ProcessRawPunchesCommand("1404/01/10"), default);

        Assert.True(res.Succeeded);
        Assert.Equal(2, res.Value);                       // دو کارمند پردازش شد
        Assert.Equal(2, records.Items.Count);

        var e1 = records.Items.Single(r => r.EmployeeId == 1);
        Assert.Equal(new TimeOnly(8, 2), e1.CheckIn);
        Assert.Equal(new TimeOnly(17, 10), e1.CheckOut);
        Assert.True(e1.WorkHours >= 8);                   // ساعتِ کار محاسبه شد

        var e2 = records.Items.Single(r => r.EmployeeId == 2);
        Assert.Equal(new TimeOnly(9, 0), e2.CheckIn);
        Assert.Null(e2.CheckOut);                         // یک ضربه → بدونِ خروج

        Assert.All(punches.Items, p => Assert.True(p.Processed));   // همه علامتِ پردازش خوردند

        // اجرای دوبارهٔ پردازش → چیزی برای پردازش نیست (idempotent).
        var again = await new ProcessRawPunchesCommandHandler(punches, records, new FakeUow(), new FakeUser())
            .Handle(new ProcessRawPunchesCommand("1404/01/10"), default);
        Assert.Equal(0, again.Value);
    }
}
