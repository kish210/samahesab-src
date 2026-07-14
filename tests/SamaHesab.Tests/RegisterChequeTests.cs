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

/// <summary>BUG-4 — ثبتِ دستیِ چک + سندِ انتقالِ دریافتنی→اسنادِ دریافتنی (چکِ دریافتی).</summary>
public class RegisterChequeTests
{
    private sealed class FakeAccountRepo : IAccountRepository
    {
        public readonly List<Account> Items = new();
        private int _seq;
        public Task AddAsync(Account e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
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

    private sealed class FakeVoucherRepo : IVoucherRepository
    {
        public readonly List<Voucher> Items = new();
        private int _seq;
        public Task AddAsync(Voucher e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => Task.FromResult((Items.Count + 1).ToString());
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Voucher?> GetWithItemsAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<List<Voucher>> GetByDateRangeAsync(int c, int fy, string f, string t, CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int c, string f, string t, CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public void Update(Voucher e) { }
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Voucher>> FindAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Voucher?> FindSingleAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Voucher> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(Voucher e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Voucher> e) { foreach (var x in e) Items.Remove(x); }
    }

    private sealed class FakeChequeRepo : IChequeRepository
    {
        public readonly List<Cheque> Items = new();
        private int _seq;
        public Task AddAsync(Cheque e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<Cheque?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<List<Cheque>> GetByStatusAsync(int companyId, ChequeStatus status, CancellationToken ct = default)
            => Task.FromResult(Items.Where(c => c.CompanyId == companyId && c.Status == status).ToList());
        public Task<List<Cheque>> GetDueTodayAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Cheque>());
        public Task<List<Cheque>> GetOverdueAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Cheque>());
        public void Update(Cheque e) { }
        public Task<List<Cheque>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Cheque>> FindAsync(Expression<Func<Cheque, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Cheque?> FindSingleAsync(Expression<Func<Cheque, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Cheque, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Cheque, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Cheque> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(Cheque e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Cheque> e) { foreach (var x in e) Items.Remove(x); }
    }

    // U-ACCT-1.7: هارنس خالی — FindAsync خالی برمی‌گرداند → FiscalYearResolver به همان fallbackِ
    // تاریخیِ ۱ می‌رسد (رفتارِ قدیمی حفظ می‌شود، این تست‌ها به رفعِ فیسکال‌یِر کاری ندارند).
    private sealed class FakeFiscalYearRepo : IRepository<FiscalYear>
    {
        public Task AddAsync(FiscalYear e, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<FiscalYear> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<FiscalYear?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<FiscalYear?>(null);
        public Task<List<FiscalYear>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<FiscalYear>());
        public Task<List<FiscalYear>> FindAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(new List<FiscalYear>());
        public Task<FiscalYear?> FindSingleAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult<FiscalYear?>(null);
        public Task<bool> AnyAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CountAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
        public void Update(FiscalYear e) { }
        public void Remove(FiscalYear e) { }
        public void RemoveRange(IEnumerable<FiscalYear> es) { }
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
        public int? UserId => 7; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "acc"; public string? FullName => "حسابدار"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static (RegisterChequeCommandHandler h, FakeChequeRepo cheques, FakeVoucherRepo vouchers, FakeAccountRepo accounts) NewSut(
        bool seedAccounts = true, bool seedPayableAccounts = false)
    {
        var acc = new FakeAccountRepo();
        if (seedAccounts)
        {
            acc.AddAsync(Account.Create(1, "1-04-001", "اسناد دریافتنی", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
            acc.AddAsync(Account.Create(1, "1-03-001", "حساب‌های دریافتنی", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        }
        if (seedPayableAccounts)
        {
            acc.AddAsync(Account.Create(1, "3-01-001", "حساب‌های پرداختنی", AccountLevel.Subsidiary, AccountNature.Credit, "بدهی")).Wait();
            acc.AddAsync(Account.Create(1, "3-02-001", "اسناد پرداختنی - چک", AccountLevel.Subsidiary, AccountNature.Credit, "بدهی")).Wait();
        }
        var cheques = new FakeChequeRepo();
        var vouchers = new FakeVoucherRepo();
        var fiscalYears = new FakeFiscalYearRepo();
        var h = new RegisterChequeCommandHandler(new FakeUow(), new FakeUser(), cheques, acc, vouchers, fiscalYears);
        return (h, cheques, vouchers, acc);
    }

    private static RegisterChequeCommand Received(decimal amount = 5_000_000) =>
        new(ChequeType.Received, "123456", "ملت", amount, "1405/05/10", PartyId: 3, PartyType: "Customer", Date: "1405/03/20");

    [Fact]
    public async Task Received_Cheque_Creates_Cheque_And_Balanced_Transfer_Voucher()
    {
        var (h, cheques, vouchers, _) = NewSut();

        var res = await h.Handle(Received(5_000_000), default);

        Assert.True(res.Succeeded);
        var c = Assert.Single(cheques.Items);
        Assert.Equal(ChequeStatus.InProcess, c.Status);
        Assert.Equal(3, c.PartyId);

        var v = Assert.Single(vouchers.Items);
        Assert.Equal(VoucherStatus.Posted, v.Status);
        Assert.True(v.IsBalanced());
        Assert.Equal(5_000_000, v.TotalDebit);
        // Dr اسناد دریافتنی (۱-۰۴-۰۰۱) / Cr حساب‌های دریافتنی (۱-۰۳-۰۰۱)
        Assert.Equal(5_000_000, v.Items.Single(i => i.Debit > 0).Debit);
        Assert.Equal(5_000_000, v.Items.Single(i => i.Credit > 0).Credit);
        Assert.Equal(v.Id, c.ReceiveVoucherId);
    }

    [Fact]
    public async Task Paid_Cheque_Without_Payable_Accounts_Creates_Cheque_Without_Voucher()
    {
        // U-ACCT-1.2: بدونِ ۳-۰۱-۰۰۱/۳-۰۲-۰۰۱، به رفتارِ قدیمی (بدونِ سند) fallback می‌کند —
        // نه اینکه چکِ پرداختنی «هرگز» سند نزند (این دیگر همیشگی نیست، فقط fallback است).
        var (h, cheques, vouchers, _) = NewSut(seedPayableAccounts: false);

        var res = await h.Handle(new RegisterChequeCommand(
            ChequeType.Paid, "777", "صادرات", 2_000_000, "1405/06/01",
            PartyId: 9, PartyType: "Supplier", Date: "1405/03/20"), default);

        Assert.True(res.Succeeded);
        Assert.Single(cheques.Items);
        Assert.Empty(vouchers.Items);
        Assert.Null(cheques.Items[0].ReceiveVoucherId);
        Assert.Null(cheques.Items[0].PayVoucherId);
    }

    [Fact]
    public async Task Paid_Cheque_With_Payable_Accounts_Posts_Reclassification_Voucher()
    {
        // U-ACCT-1.2: صدورِ چکِ پرداختنی حالا بدهی را از پرداختنیِ عمومی (۳-۰۱-۰۰۱) به اسنادِ
        // پرداختنی-چک (۳-۰۲-۰۰۱) بازطبقه‌بندی می‌کند تا وصولِ بعدی (ChangeChequeStatusCommand)
        // از حسابِ درستی برداشت کند.
        var (h, cheques, vouchers, accounts) = NewSut(seedPayableAccounts: true);

        var res = await h.Handle(new RegisterChequeCommand(
            ChequeType.Paid, "777", "صادرات", 2_000_000, "1405/06/01",
            PartyId: 9, PartyType: "Supplier", Date: "1405/03/20"), default);

        Assert.True(res.Succeeded);
        var cheque = Assert.Single(cheques.Items);
        var v = Assert.Single(vouchers.Items);
        Assert.True(v.IsBalanced());
        Assert.Equal(v.Id, cheque.PayVoucherId);
        var generalPayable = accounts.Items.Single(a => a.Code == "3-01-001");
        var notesPayable = accounts.Items.Single(a => a.Code == "3-02-001");
        Assert.Equal(2_000_000m, v.Items.Single(i => i.AccountId == generalPayable.Id).Debit);
        Assert.Equal(2_000_000m, v.Items.Single(i => i.AccountId == notesPayable.Id).Credit);
    }

    [Fact]
    public async Task Received_Without_Accounts_Fails_And_Posts_Nothing()
    {
        var (h, cheques, vouchers, _) = NewSut(seedAccounts: false);

        var res = await h.Handle(Received(), default);

        Assert.False(res.Succeeded);
        Assert.Empty(cheques.Items);
        Assert.Empty(vouchers.Items);
    }

    [Fact]
    public void Validator_Rejects_NonPositive_Amount_And_Empty_Number()
    {
        var v = new RegisterChequeCommandValidator();
        Assert.False(v.Validate(Received(0)).IsValid);
        Assert.False(v.Validate(new RegisterChequeCommand(
            ChequeType.Received, "", "ملت", 1000, "1405/05/10", 3, "Customer", "1405/03/20")).IsValid);
        Assert.True(v.Validate(Received(1000)).IsValid);
    }
}
