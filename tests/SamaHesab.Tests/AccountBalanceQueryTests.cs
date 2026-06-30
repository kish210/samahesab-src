using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Treasury.Queries;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>AUDIT-3 — کوئریِ ماندهٔ حساب بر اساسِ کد (برای هشدارِ اضافه‌برداشتِ صندوق).</summary>
public class AccountBalanceQueryTests
{
    private sealed class FakeAccountRepo : IAccountRepository
    {
        public readonly List<Account> Items = new();
        public decimal BalanceToReturn;
        public int? BalanceAskedFor;

        public Task<Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.CompanyId == companyId && x.Code == code));
        public Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default)
        { BalanceAskedFor = accountId; return Task.FromResult(BalanceToReturn); }

        // بقیه استفاده نمی‌شوند
        public Task AddAsync(Account e, CancellationToken ct = default) { Items.Add(e); return Task.CompletedTask; }
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public void Update(Account e) { }
        public Task<List<Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> GetChildrenAsync(int parentId, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<List<Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<List<Account>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> FindAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Account?> FindSingleAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Account> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(Account e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Account> e) { }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    [Fact]
    public async Task Returns_Balance_For_Resolved_Account_Code()
    {
        var repo = new FakeAccountRepo { BalanceToReturn = -456_994_140m };
        var cash = Account.Create(1, "1-01-001", "صندوق",
            SamaHesab.Domain.Enums.AccountLevel.Subsidiary, SamaHesab.Domain.Enums.AccountNature.Debit, "دارایی");
        typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(cash, 52);
        repo.Items.Add(cash);

        var res = await new GetAccountBalanceQueryHandler(repo, new FakeUser())
            .Handle(new GetAccountBalanceQuery("1-01-001"), default);

        Assert.Equal(-456_994_140m, res);
        Assert.Equal(52, repo.BalanceAskedFor);   // ماندهٔ همان حسابِ resolve‌شده پرسیده شد
    }

    [Fact]
    public async Task Unknown_Code_Returns_Zero()
    {
        var res = await new GetAccountBalanceQueryHandler(new FakeAccountRepo { BalanceToReturn = 999m }, new FakeUser())
            .Handle(new GetAccountBalanceQuery("9-99-999"), default);
        Assert.Equal(0m, res);
    }
}
