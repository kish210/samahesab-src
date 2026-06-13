using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ایجاد/ویرایشِ حساب در نمودار حساب‌ها (CRUDِ هستهٔ ERP) — `SaveAccountCommand`.</summary>
public class SaveAccountCommandTests
{
    // ── فیک‌های حداقلی برای مسیرِ این هندلر ──
    private sealed class FakeAccountRepo : IAccountRepository
    {
        public readonly List<Account> Items = new();
        private int _seq;

        public Task AddAsync(Account e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.CompanyId == companyId && x.Code == code));
        public void Update(Account e) { }

        public Task<List<Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> GetChildrenAsync(int parentId, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<List<Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default) => Task.FromResult(0m);
        public Task<List<Account>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> FindAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Account?> FindSingleAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Account> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(Account e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Account> e) { foreach (var x in e) Items.Remove(x); }
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
        public string? Username => "admin"; public string? FullName => "ادمین"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static SaveAccountCommandHandler NewHandler(FakeAccountRepo repo)
        => new(repo, new FakeUow(), new FakeUser());

    [Fact]
    public async Task Create_NewAccount_Persists_With_Level_From_Parent()
    {
        var repo = new FakeAccountRepo();
        // والدِ سطحِ کل (General=2)
        await repo.AddAsync(Account.Create(1, "1-01", "دارایی‌های جاری", AccountLevel.General, AccountNature.Debit, "دارایی"));
        var parentId = repo.Items[0].Id;
        var sut = NewHandler(repo);

        var res = await sut.Handle(new SaveAccountCommand(null, "1-01-001", "صندوق", "بدهکار", "دارایی", parentId), default);

        Assert.True(res.Succeeded);
        var created = repo.Items.Single(a => a.Code == "1-01-001");
        Assert.Equal(AccountLevel.Subsidiary, created.Level);   // والد(۲)+۱ = معین(۳)
        Assert.Equal(AccountNature.Debit, created.Nature);
    }

    [Fact]
    public async Task Create_Duplicate_Code_Fails()
    {
        var repo = new FakeAccountRepo();
        await repo.AddAsync(Account.Create(1, "1-01-001", "صندوق", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی"));
        var sut = NewHandler(repo);

        var res = await sut.Handle(new SaveAccountCommand(null, "1-01-001", "صندوق دوم", "بدهکار", "دارایی", null), default);

        Assert.False(res.Succeeded);
        Assert.Equal(1, repo.Items.Count(a => a.Code == "1-01-001"));
    }

    [Fact]
    public async Task Edit_Updates_Name_And_Nature()
    {
        var repo = new FakeAccountRepo();
        await repo.AddAsync(Account.Create(1, "4-01", "فروش", AccountLevel.General, AccountNature.Debit, "درآمد"));
        var id = repo.Items[0].Id;
        var sut = NewHandler(repo);

        var res = await sut.Handle(new SaveAccountCommand(id, "4-01", "فروش کالا", "بستانکار", "درآمد", null), default);

        Assert.True(res.Succeeded);
        Assert.Equal("فروش کالا", repo.Items[0].Name);
        Assert.Equal(AccountNature.Credit, repo.Items[0].Nature);
    }
}
