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

/// <summary>🟡 آمادگی — حذفِ کارمند: سابقهٔ فیش/تردد → غیرفعال‌سازیِ نرم (نه حذفِ یتیم‌کننده).</summary>
public class DeleteEmployeeGuardTests
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

    private static (FakeRepo<Employee> emps, Employee emp) Seed()
    {
        var emps = new FakeRepo<Employee>();
        var e = Employee.Create(1, 1, "E1", "001", "علی", "احمدی", "1404/01/01", 10_000_000m);
        emps.AddAsync(e).Wait();   // Id=1
        return (emps, e);
    }

    [Fact]
    public async Task Delete_With_Payroll_History_Deactivates_Not_Removes()
    {
        var (emps, emp) = Seed();
        var slips = new FakeRepo<SalarySlip>();
        slips.AddAsync(SalarySlip.Create(emp.Id, "1404", 1, 10_000_000m)).Wait();   // سابقهٔ فیش
        var att = new FakeRepo<AttendanceRecord>();

        var res = await new DeleteEmployeeCommandHandler(emps, slips, att, new FakeUow(), new FakeUser())
            .Handle(new DeleteEmployeeCommand(emp.Id), default);

        Assert.False(res.Succeeded);                 // پیامِ «غیرفعال شد»
        Assert.Single(emps.Items);                   // حذف نشد
        Assert.False(emps.Items[0].IsActive);        // غیرفعال شد
    }

    [Fact]
    public async Task Delete_Without_History_Removes()
    {
        var (emps, emp) = Seed();
        var res = await new DeleteEmployeeCommandHandler(emps, new FakeRepo<SalarySlip>(),
            new FakeRepo<AttendanceRecord>(), new FakeUow(), new FakeUser())
            .Handle(new DeleteEmployeeCommand(emp.Id), default);

        Assert.True(res.Succeeded);
        Assert.Empty(emps.Items);                    // حذفِ سخت (بدونِ سابقه)
    }
}
