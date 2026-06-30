using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Commands;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>P3 — شعبهٔ مالکِ طرف‌حساب روی دادهٔ پایه: تگ‌خوردنِ مشتریِ نو با شعبهٔ سازنده
/// و فیلترِ اختیاریِ شعبه (مالکِ همان شعبه + مشترک‌ها) بدونِ تغییرِ رفتارِ پیش‌فرض.</summary>
public class PartyBranchScopeTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default) { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { } public void Remove(T e) => Items.Remove(e); public void RemoveRange(IEnumerable<T> e) { }
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
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId { get; init; } = 2;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static CreateCustomerCommand NewCust(string code) => new(
        code, "حقیقی", "علی", "احمدی", null, null, "0912", null, null, null, null, null,
        0, 0, "خرده", 0, NationalCode: null, EconomicCode: null, GroupId: null, Notes: null,
        ContactPerson: null, Visitor: null, BirthDate: null);

    [Fact]
    public async Task New_Customer_Is_Tagged_With_Creators_Branch()
    {
        var parties = new FakeRepo<Party>();
        var h = new CreateCustomerCommandHandler(parties, new FakeUow(), new FakeUser { BranchId = 5 });

        var res = await h.Handle(NewCust("C1"), default);

        Assert.True(res.Succeeded);
        Assert.Equal(5, parties.Items.Single().BranchId);
    }

    [Fact]
    public async Task Branch_Filter_Returns_Own_Branch_Plus_Shared_Only()
    {
        var parties = new FakeRepo<Party>();
        Party Mk(string code, int? branch) { var p = Party.Create(1, code, "حقیقی", code, "x", isCustomer: true); p.SetBranch(branch); return p; }
        await parties.AddAsync(Mk("A", 2));     // شعبهٔ ۲
        await parties.AddAsync(Mk("B", 3));     // شعبهٔ ۳
        await parties.AddAsync(Mk("S", null));  // مشترک
        var h = new GetCustomersQueryHandler(parties, new FakeUser());

        var branch2 = await h.Handle(new GetCustomersQuery(BranchId: 2), default);
        Assert.Equal(2, branch2.Count);                          // A(2) + S(shared)
        Assert.DoesNotContain(branch2, r => r.Code == "B");

        var all = await h.Handle(new GetCustomersQuery(), default);   // پیش‌فرض = همه (بدونِ تغییرِ رفتار)
        Assert.Equal(3, all.Count);
    }
}
