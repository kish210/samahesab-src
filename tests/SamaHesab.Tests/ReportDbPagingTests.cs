using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-DB-PAGING (@2026-07-16) — کوئری‌هایِ گزارش دیگر کلِ بازهٔ تاریخ را در حافظه بارگذاری
/// نمی‌کنند؛ فیلتر/مرتب‌سازی/جمع‌بندی از رویِ متدهایِ نویِ ریپازیتوری (که در EF واقعاً به SQL
/// ترجمه می‌شوند) انجام می‌شود. این فایل همان متدها را با یک FakeVoucherRepوی کاملِ خودش تست می‌کند.</summary>
public class ReportDbPagingTests
{
    private sealed class FakeVoucherRepo : IVoucherRepository
    {
        public List<Voucher> Vouchers { get; } = new();

        public Task<(List<Voucher> Items, int TotalCount)> GetPagedByDateRangeAsync(
            int companyId, int fiscalYearId, string fromDate, string toDate,
            int? status, string? searchText, int pageNumber, int pageSize, CancellationToken ct = default)
        {
            var query = Vouchers.Where(v => v.CompanyId == companyId && v.FiscalYearId == fiscalYearId
                && string.Compare(v.VoucherDate, fromDate) >= 0 && string.Compare(v.VoucherDate, toDate) <= 0);
            if (status.HasValue) query = query.Where(v => (int)v.Status == status.Value);
            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(v => v.VoucherNumber.Contains(searchText)
                    || (v.Description != null && v.Description.Contains(searchText)));
            var total = query.Count();
            var items = query.OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.VoucherNumber)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult((items, total));
        }

        public Task<decimal> SumAccountMovementBeforeAsync(int companyId, int accountId, string beforeDate, CancellationToken ct = default)
        {
            var sum = Vouchers.Where(v => v.CompanyId == companyId && !v.IsReversed
                    && string.Compare(v.VoucherDate, beforeDate) < 0)
                .SelectMany(v => v.Items).Where(i => i.AccountId == accountId)
                .Sum(i => i.Debit - i.Credit);
            return Task.FromResult(sum);
        }

        public Task<List<VoucherItem>> GetAccountItemsInRangeAsync(int companyId, int accountId, string fromDate, string toDate, CancellationToken ct = default)
        {
            var items = Vouchers.Where(v => v.CompanyId == companyId && !v.IsReversed
                    && string.Compare(v.VoucherDate, fromDate) >= 0 && string.Compare(v.VoucherDate, toDate) <= 0)
                .SelectMany(v => v.Items).Where(i => i.AccountId == accountId)
                .OrderBy(i => i.Voucher!.VoucherDate).ThenBy(i => i.Voucher!.VoucherNumber)
                .ToList();
            return Task.FromResult(items);
        }

        public Task<List<AccountMovementTotal>> GetAccountTotalsInRangeAsync(int companyId, string fromDate, string toDate,
            int? costCenterId, int? projectId, int? branchId, CancellationToken ct = default)
        {
            var totals = Vouchers.Where(v => v.CompanyId == companyId
                    && string.Compare(v.VoucherDate, fromDate) >= 0 && string.Compare(v.VoucherDate, toDate) <= 0
                    && (branchId == null || v.BranchId == branchId))
                .SelectMany(v => v.Items)
                .Where(i => (costCenterId == null || i.CostCenterId == costCenterId)
                         && (projectId == null || i.ProjectId == projectId))
                .GroupBy(i => i.AccountId)
                .Select(g => new AccountMovementTotal(g.Key, g.Sum(x => x.Debit), g.Sum(x => x.Credit)))
                .ToList();
            return Task.FromResult(totals);
        }

        public Task<List<VoucherItem>> GetLedgerItemsInRangeAsync(int companyId, string fromDate, string toDate,
            int? accountId, int? costCenterId, int? projectId, int? branchId, CancellationToken ct = default)
        {
            var items = Vouchers.Where(v => v.CompanyId == companyId
                    && string.Compare(v.VoucherDate, fromDate) >= 0 && string.Compare(v.VoucherDate, toDate) <= 0
                    && (branchId == null || v.BranchId == branchId))
                .SelectMany(v => v.Items)
                .Where(i => (accountId == null || i.AccountId == accountId)
                         && (costCenterId == null || i.CostCenterId == costCenterId)
                         && (projectId == null || i.ProjectId == projectId))
                .OrderBy(i => i.Voucher!.VoucherDate).ThenBy(i => i.Voucher!.VoucherNumber)
                .ToList();
            return Task.FromResult(items);
        }

        public Task<List<Voucher>> GetByDateRangeAsync(int companyId, int fiscalYearId, string fromDate, string toDate, CancellationToken ct = default)
            => throw new System.NotImplementedException();
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int companyId, string fromDate, string toDate, CancellationToken ct = default)
            => throw new System.NotImplementedException();
        public Task<Voucher?> GetWithItemsAsync(int voucherId, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task AddAsync(Voucher e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, Vouchers.Count + 1); Vouchers.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Voucher> es, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Vouchers.FirstOrDefault(v => v.Id == id));
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Vouchers.ToList());
        public Task<List<Voucher>> FindAsync(Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Vouchers.AsQueryable().Where(p).ToList());
        public Task<Voucher?> FindSingleAsync(Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Vouchers.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Vouchers.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Vouchers.AsQueryable().Count(p));
        public void Update(Voucher e) { }
        public void Remove(Voucher e) => Vouchers.Remove(e);
        public void RemoveRange(IEnumerable<Voucher> es) { }
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<Account> Items { get; } = new();
        public Task<List<Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.Where(a => a.CompanyId == companyId).ToList());
        public Task<Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(a => a.CompanyId == companyId && a.Code == code));
        public Task<List<Account>> GetChildrenAsync(int parentId, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<List<Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default) => Task.FromResult(0m);
        public Task AddAsync(Account e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, Items.Count + 1); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Account> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
        public Task<List<Account>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> FindAsync(Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Account?> FindSingleAsync(Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(Account e) { }
        public void Remove(Account e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Account> es) { }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public int? SalespersonPartyId => null;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private sealed class FakeUserRepo : IRepository<User>
    {
        public Task<List<User>> FindAsync(Expression<System.Func<User, bool>> p, CancellationToken ct = default) => Task.FromResult(new List<User>());
        public Task AddAsync(User e, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task AddRangeAsync(IEnumerable<User> es, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<User?> GetByIdAsync(int id, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<List<User>> GetAllAsync(CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<User?> FindSingleAsync(Expression<System.Func<User, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<bool> AnyAsync(Expression<System.Func<User, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<int> CountAsync(Expression<System.Func<User, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public void Update(User e) { }
        public void Remove(User e) { }
        public void RemoveRange(IEnumerable<User> es) { }
    }

    // در EF واقعی، Include(i => i.Voucher) این navigation را پر می‌کند؛ این‌جا با reflection شبیه‌سازی می‌شود
    // چون VoucherItem.Voucher ستِ خصوصی دارد و FakeRepoها روی همان الگویِ اسنادِ Domain کار می‌کنند.
    private static Voucher MakeVoucher(int companyId, string number, string date, int cashAccountId, int salesAccountId, decimal amount)
    {
        var v = Voucher.Create(companyId, 1, 1, number, date, 3);
        var voucherProp = typeof(VoucherItem).GetProperty(nameof(VoucherItem.Voucher))!;
        var item1 = VoucherItem.Create(0, 1, cashAccountId, amount, 0);
        var item2 = VoucherItem.Create(0, 2, salesAccountId, 0, amount);
        v.AddItem(item1);
        v.AddItem(item2);
        voucherProp.SetValue(item1, v);
        voucherProp.SetValue(item2, v);
        return v;
    }

    [Fact]
    public async Task GetVouchersQuery_Paginates_At_DB_Level_With_Correct_Total()
    {
        var repo = new FakeVoucherRepo();
        for (int i = 1; i <= 12; i++)
            await repo.AddAsync(MakeVoucher(1, $"V{i:D3}", $"1405/01/{i:D2}", 1, 2, 1000 * i));

        var handler = new GetVouchersQueryHandler(repo, new FakeUser(), new FakeUserRepo());
        var page1 = await handler.Handle(new GetVouchersQuery(1, "1405/01/01", "1405/01/31", PageNumber: 1, PageSize: 5), default);
        var page2 = await handler.Handle(new GetVouchersQuery(1, "1405/01/01", "1405/01/31", PageNumber: 2, PageSize: 5), default);
        var page3 = await handler.Handle(new GetVouchersQuery(1, "1405/01/01", "1405/01/31", PageNumber: 3, PageSize: 5), default);

        Assert.Equal(12, page1.TotalCount);
        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(5, page2.Items.Count);
        Assert.Equal(2, page3.Items.Count);
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
        // مرتب‌سازیِ نزولی بر اساسِ تاریخ: صفحهٔ اول باید جدیدترین سند (V012) را داشته باشد.
        Assert.Equal("V012", page1.Items[0].VoucherNumber);
    }

    [Fact]
    public async Task GetAccountLedgerQuery_Computes_Opening_Balance_And_Rows_Via_Repository_Methods()
    {
        var repo = new FakeVoucherRepo();
        // سه سند: دو تا پیش از FromDate (ماندهٔ ابتدا)، یکی در بازه.
        await repo.AddAsync(MakeVoucher(1, "V001", "1405/01/01", 10, 20, 1000));
        await repo.AddAsync(MakeVoucher(1, "V002", "1405/01/05", 10, 20, 500));
        await repo.AddAsync(MakeVoucher(1, "V003", "1405/02/01", 10, 20, 300));

        var handler = new GetAccountLedgerQueryHandler(repo, new FakeUser());
        var result = await handler.Handle(new GetAccountLedgerQuery(10, "1405/02/01", "1405/12/29"), default);

        Assert.Equal(1500m, result.OpeningBalance);
        Assert.Single(result.Rows);
        Assert.Equal(300m, result.Rows[0].Debit);
        Assert.Equal(1800m, result.ClosingBalance);
    }

    [Fact]
    public async Task GetTrialBalanceQuery_Groups_By_Account_Via_Repository_Method()
    {
        var repo = new FakeVoucherRepo();
        var accounts = new FakeAccountRepo();
        await accounts.AddAsync(Account.Create(1, "1-01-001", "صندوق", Domain.Enums.AccountLevel.Subsidiary, Domain.Enums.AccountNature.Debit, "دارایی"));
        await accounts.AddAsync(Account.Create(1, "6-01-001", "درآمد", Domain.Enums.AccountLevel.Subsidiary, Domain.Enums.AccountNature.Credit, "درآمد"));
        await repo.AddAsync(MakeVoucher(1, "V001", "1405/01/01", 1, 2, 1000));
        await repo.AddAsync(MakeVoucher(1, "V002", "1405/01/02", 1, 2, 500));

        var handler = new GetTrialBalanceQueryHandler(repo, accounts, new FakeUser());
        var rows = await handler.Handle(new GetTrialBalanceQuery("1405/01/01", "1405/12/29"), default);

        Assert.Equal(2, rows.Count);
        var cash = rows.Single(r => r.Code == "1-01-001");
        Assert.Equal(1500m, cash.Debit);
        Assert.Equal(0m, cash.Credit);
    }

    [Fact]
    public async Task GetGeneralLedgerQuery_Filters_By_AccountId_Via_Repository_Method()
    {
        var repo = new FakeVoucherRepo();
        var accounts = new FakeAccountRepo();
        await accounts.AddAsync(Account.Create(1, "1-01-001", "صندوق", Domain.Enums.AccountLevel.Subsidiary, Domain.Enums.AccountNature.Debit, "دارایی"));
        await accounts.AddAsync(Account.Create(1, "6-01-001", "درآمد", Domain.Enums.AccountLevel.Subsidiary, Domain.Enums.AccountNature.Credit, "درآمد"));
        await repo.AddAsync(MakeVoucher(1, "V001", "1405/01/01", 1, 2, 1000));

        var handler = new GetGeneralLedgerQueryHandler(repo, accounts, new FakeUser());
        var allRows = await handler.Handle(new GetGeneralLedgerQuery("1405/01/01", "1405/12/29", AccountId: null), default);
        var cashRows = await handler.Handle(new GetGeneralLedgerQuery("1405/01/01", "1405/12/29", AccountId: 1), default);

        Assert.Equal(2, allRows.Count);
        Assert.Single(cashRows);
        Assert.Equal("1-01-001", cashRows[0].Code);
    }
}
