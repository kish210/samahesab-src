using System.Linq.Expressions;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>type-ahead جست‌وجوی حساب برای ورود سریع سند — قلب کیبوردمحوری حسابداری.</summary>
public class SearchAccountsQueryTests
{
    private static Account Leaf(string code, string name, bool active = true)
    {
        var a = Account.Create(companyId: 1, code: code, name: name,
            level: AccountLevel.Subsidiary, nature: AccountNature.Debit, accountType: "دارایی");
        if (!active) a.Deactivate();
        return a;
    }

    private static SearchAccountsQueryHandler HandlerWith(params Account[] leaves)
        => new(new FakeAccountRepository(leaves.ToList()), new FakeCurrentUser());

    [Fact]
    public async Task Empty_Term_Returns_All_Active_Leaves()
    {
        var h = HandlerWith(Leaf("1001", "صندوق"), Leaf("1002", "بانک ملت"));
        var result = await h.Handle(new SearchAccountsQuery(""), default);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Filters_By_Code_Fragment()
    {
        var h = HandlerWith(Leaf("1001", "صندوق"), Leaf("2001", "حساب پرداختنی"));
        var result = await h.Handle(new SearchAccountsQuery("100"), default);
        Assert.Single(result);
        Assert.Equal("1001", result[0].Code);
    }

    [Fact]
    public async Task Filters_By_Name_Fragment()
    {
        var h = HandlerWith(Leaf("1001", "صندوق"), Leaf("1002", "بانک ملت"));
        var result = await h.Handle(new SearchAccountsQuery("بانک"), default);
        Assert.Single(result);
        Assert.Equal("1002", result[0].Code);
    }

    [Fact]
    public async Task Excludes_Inactive_Accounts()
    {
        var h = HandlerWith(Leaf("1001", "صندوق"), Leaf("1009", "صندوق قدیمی", active: false));
        var result = await h.Handle(new SearchAccountsQuery("صندوق"), default);
        Assert.Single(result);
        Assert.Equal("1001", result[0].Code);
    }

    [Fact]
    public async Task Code_Prefix_Match_Ranks_First()
    {
        // "200" به‌عنوان بخشی از کد در 1200 هست، ولی 2001 با 200 شروع می‌شود → باید اول بیاید
        var h = HandlerWith(Leaf("1200", "تنخواه"), Leaf("2001", "پرداختنی"));
        var result = await h.Handle(new SearchAccountsQuery("200"), default);
        Assert.Equal("2001", result[0].Code);
    }

    [Fact]
    public async Task Honors_MaxResults()
    {
        var many = Enumerable.Range(1, 50).Select(i => Leaf($"10{i:00}", $"حساب {i}")).ToArray();
        var h = HandlerWith(many);
        var result = await h.Handle(new SearchAccountsQuery("حساب", MaxResults: 5), default);
        Assert.Equal(5, result.Count);
    }

    // ─── Fakes ──────────────────────────────────────────────────────────────
    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public int? UserId => 1;
        public int? CompanyId => 1;
        public int? BranchId => 1;
        public string? Username => "test";
        public string? FullName => "تست";
        public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => Array.Empty<string>();
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        private readonly List<Account> _leaves;
        public FakeAccountRepository(List<Account> leaves) => _leaves = leaves;

        public Task<List<Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default)
            => Task.FromResult(_leaves);

        // اعضای استفاده‌نشده در این تست:
        public Task<List<Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Account>> GetChildrenAsync(int parentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Account>> GetAllAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<Account>> FindAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Account?> FindSingleAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> AnyAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(Account e, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<Account> e, CancellationToken ct = default) => throw new NotSupportedException();
        public void Update(Account e) => throw new NotSupportedException();
        public void Remove(Account e) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<Account> e) => throw new NotSupportedException();
    }
}
