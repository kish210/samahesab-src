using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-ACCT-DRILLDOWN — تراز آزمایشی حالا AccountId هر ردیف را برمی‌گرداند تا دوبارکلیک
/// در FinancialReportsView بتواند مستقیم به دفترِ معینِ همان حساب برود.</summary>
public class TrialBalanceDrillDownTests
{
    private sealed class FakeVoucherRepo : IVoucherRepository
    {
        public List<Voucher> Vouchers { get; } = new();
        public Task<List<Voucher>> GetByDateRangeAsync(int companyId, int fiscalYearId, string fromDate, string toDate, CancellationToken ct = default)
            => throw new System.NotImplementedException();
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int companyId, string fromDate, string toDate, CancellationToken ct = default)
            => Task.FromResult(Vouchers.Where(v => v.CompanyId == companyId).ToList());
        public Task<Voucher?> GetWithItemsAsync(int voucherId, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task AddAsync(Voucher e, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Voucher> es, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<List<Voucher>> FindAsync(System.Linq.Expressions.Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<Voucher?> FindSingleAsync(System.Linq.Expressions.Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<int> CountAsync(System.Linq.Expressions.Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public void Update(Voucher e) { }
        public void Remove(Voucher e) { }
        public void RemoveRange(IEnumerable<Voucher> es) { }
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<Account> Accounts { get; } = new();
        public Task<List<Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default)
            => Task.FromResult(Accounts.Where(a => a.CompanyId == companyId).ToList());
        public Task<Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<List<Account>> GetChildrenAsync(int parentId, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<List<Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task AddAsync(Account e, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task AddRangeAsync(IEnumerable<Account> es, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<List<Account>> GetAllAsync(CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<List<Account>> FindAsync(System.Linq.Expressions.Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<Account?> FindSingleAsync(System.Linq.Expressions.Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public Task<int> CountAsync(System.Linq.Expressions.Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => throw new System.NotImplementedException();
        public void Update(Account e) { }
        public void Remove(Account e) { }
        public void RemoveRange(IEnumerable<Account> es) { }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "admin"; public string? FullName => "مدیر"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    [Fact]
    public async Task TrialBalance_Rows_Carry_AccountId_For_DrillDown()
    {
        var accounts = new FakeAccountRepo();
        var cash = Account.Create(1, "1-01-001", "صندوق", AccountLevel.Detail, AccountNature.Debit, "دارایی");
        var revenue = Account.Create(1, "6-01-001", "فروش", AccountLevel.Detail, AccountNature.Credit, "درآمد");
        typeof(SamaHesab.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(cash, 10);
        typeof(SamaHesab.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(revenue, 20);
        accounts.Accounts.Add(cash);
        accounts.Accounts.Add(revenue);

        var voucherRepo = new FakeVoucherRepo();
        var v = Voucher.Create(1, 1, 1, "V-1", "1405/01/01", 1);
        typeof(SamaHesab.Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(v, 100);
        v.AddItem(VoucherItem.Create(v.Id, 1, cash.Id, debit: 5000, credit: 0));
        v.AddItem(VoucherItem.Create(v.Id, 2, revenue.Id, debit: 0, credit: 5000));
        voucherRepo.Vouchers.Add(v);

        var handler = new GetTrialBalanceQueryHandler(voucherRepo, accounts, new FakeUser());
        var rows = await handler.Handle(new GetTrialBalanceQuery("1405/01/01", "1405/12/29"), default);

        Assert.Equal(2, rows.Count);
        var cashRow = rows.Single(r => r.Code == "1-01-001");
        var revenueRow = rows.Single(r => r.Code == "6-01-001");
        Assert.Equal(cash.Id, cashRow.AccountId);
        Assert.Equal(revenue.Id, revenueRow.AccountId);
    }
}
