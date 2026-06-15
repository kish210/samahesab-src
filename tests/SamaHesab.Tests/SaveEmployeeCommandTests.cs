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

/// <summary>M7 — ذخیره/فهرستِ کارکنان (`SaveEmployeeCommand`/`GetEmployeesQuery`).</summary>
public class SaveEmployeeCommandTests
{
    private sealed class FakeRepo : IRepository<Employee>
    {
        public readonly List<Employee> Items = new();
        private int _seq;
        public Task AddAsync(Employee e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<Employee?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<List<Employee>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Employee>> FindAsync(Expression<Func<Employee, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Employee?> FindSingleAsync(Expression<Func<Employee, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Employee, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Employee, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Employee> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Update(Employee e) { }
        public void Remove(Employee e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Employee> e) { foreach (var x in e) Items.Remove(x); }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public int Saves;
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "admin"; public string? FullName => "ادمین"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static SaveEmployeeCommand New(int id = 0, string nc = "0012345678", decimal salary = 12_000_000m)
        => new(id, "E1", nc, "علی", "احمدی", "1403/01/01", salary, "دائم", Mobile: "0912");

    [Fact]
    public async Task Create_Persists_Employee()
    {
        var repo = new FakeRepo(); var uow = new FakeUow();
        var sut = new SaveEmployeeCommandHandler(repo, uow, new FakeUser());

        var res = await sut.Handle(New(), default);

        Assert.True(res.Succeeded);
        var emp = Assert.Single(repo.Items);
        Assert.Equal("علی احمدی", emp.FullName);
        Assert.Equal(12_000_000m, emp.BaseSalary);
        Assert.Equal("0912", emp.Mobile);
        Assert.True(uow.Saves > 0);                 // واقعاً ذخیره شد (نه استاب)
    }

    [Fact]
    public async Task Edit_Updates_Existing_Salary()
    {
        var repo = new FakeRepo(); var uow = new FakeUow();
        var sut = new SaveEmployeeCommandHandler(repo, uow, new FakeUser());
        await sut.Handle(New(), default);
        var id = repo.Items[0].Id;

        var res = await sut.Handle(New(id: id, salary: 18_000_000m), default);

        Assert.True(res.Succeeded);
        Assert.Single(repo.Items);                  // رکوردِ جدید ساخته نشد
        Assert.Equal(18_000_000m, repo.Items[0].BaseSalary);
    }

    [Fact]
    public async Task GetEmployees_Filters_And_Maps()
    {
        var repo = new FakeRepo(); var uow = new FakeUow();
        var save = new SaveEmployeeCommandHandler(repo, uow, new FakeUser());
        await save.Handle(New(nc: "111"), default);
        await save.Handle(New(nc: "222"), default);

        var list = await new GetEmployeesQueryHandler(repo, new FakeUser()).Handle(new GetEmployeesQuery(), default);

        Assert.Equal(2, list.Count);
        Assert.All(list, e => Assert.True(e.IsActive));
        Assert.Contains(list, e => e.FullName == "علی احمدی");
    }
}
