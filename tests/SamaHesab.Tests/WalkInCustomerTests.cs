using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Commands;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>POS-CUST-1 — پیش‌تر POS/رستوران بدونِ انتخابِ مشتری CustomerId=۱ هاردکد می‌فرستادند (نه یک
/// «متفرقه»ی واقعی)؛ این طرف‌حسابِ اختصاصی بارِ اول ساخته و دفعاتِ بعد بازیابی می‌شود (نه تکراری‌سازی).</summary>
public class WalkInCustomerTests
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
        public IRepository<T> GetRepository<T>() where T : class => throw new System.NotImplementedException();
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
    public async Task First_Call_Creates_WalkIn_Party()
    {
        var parties = new FakeRepo<Party>();
        var handler = new GetOrCreateWalkInCustomerCommandHandler(parties, new FakeUow(), new FakeUser());

        var id = await handler.Handle(new GetOrCreateWalkInCustomerCommand(), default);

        var party = Assert.Single(parties.Items);
        Assert.Equal(id, party.Id);
        Assert.Equal(GetOrCreateWalkInCustomerCommandHandler.WalkInCode, party.Code);
        Assert.True(party.IsCustomer);
    }

    [Fact]
    public async Task Second_Call_Reuses_Existing_WalkIn_Party_Not_Duplicate()
    {
        var parties = new FakeRepo<Party>();
        var handler = new GetOrCreateWalkInCustomerCommandHandler(parties, new FakeUow(), new FakeUser());

        var id1 = await handler.Handle(new GetOrCreateWalkInCustomerCommand(), default);
        var id2 = await handler.Handle(new GetOrCreateWalkInCustomerCommand(), default);

        Assert.Equal(id1, id2);
        Assert.Single(parties.Items);   // نه دو رکورد
    }
}
