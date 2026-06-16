using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Licensing;
using SamaHesab.Application.Settings.Commands;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ P-G7 — اعمالِ سقفِ شعبهٔ رده (SaveBranchCommand × ILicenseContext).</summary>
public class LicenseEnforcementTests
{
    private sealed class InMemoryRepo<T> : IRepository<T> where T : BaseEntity
    {
        private readonly List<T> _items = new(); private int _seq;
        public IReadOnlyList<T> Items => _items;
        public Task AddAsync(T e, CancellationToken ct = default) { typeof(BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); _items.Add(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().FirstOrDefault(p));
        public void Update(T e) { }
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Where(p).ToList());
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { _items.AddRange(e); return Task.CompletedTask; }
        public void Remove(T e) => _items.Remove(e); public void RemoveRange(IEnumerable<T> e) { foreach (var x in e) _items.Remove(x); }
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
        public string? Username => "admin"; public string? FullName => "Admin"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }
    private sealed class FakeLicense : ILicenseContext
    {
        public bool IsTrial { get; init; }
        public int MaxBranches { get; init; } = LicenseLimits.Unlimited;
        public int MaxUsers { get; init; } = LicenseLimits.Unlimited;
        public int TrialVoucherLimit { get; init; } = 200;
    }

    private static SaveBranchCommand NewBranch(string code) => new(0, code, "شعبه " + code, null, null, null, false);

    [Fact]
    public async Task Starter_Allows_One_Branch_Then_Blocks()
    {
        var repo = new InMemoryRepo<Branch>();
        var sut = new SaveBranchCommandHandler(repo, new FakeUow(), new FakeUser(), new FakeLicense { MaxBranches = 1 });

        var first = await sut.Handle(NewBranch("B1"), default);
        Assert.True(first.Succeeded);

        var second = await sut.Handle(NewBranch("B2"), default);
        Assert.False(second.Succeeded);
        Assert.Contains("پر شده", second.ErrorMessage);   // پیامِ سقفِ شعبه
        Assert.Single(repo.Items);   // شعبهٔ دوم ساخته نشد
    }

    [Fact]
    public async Task Enterprise_Unlimited_Allows_Many()
    {
        var repo = new InMemoryRepo<Branch>();
        var sut = new SaveBranchCommandHandler(repo, new FakeUow(), new FakeUser(), new FakeLicense { MaxBranches = LicenseLimits.Unlimited });

        for (int i = 0; i < 5; i++) Assert.True((await sut.Handle(NewBranch("B" + i), default)).Succeeded);
    }
}
