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

/// <summary>U-ACCT-1.2 — وصولِ چکِ پرداختنی باید از حسابی برداشت کند که بدهی‌اش واقعاً همان‌جاست:
/// چکِ نو (که RegisterChequeCommand بدهی‌اش را به ۳-۰۲-۰۰۱ بازطبقه‌بندی کرده، PayVoucherId ست شده)
/// از ۳-۰۲-۰۰۱، چکِ قدیمیِ پیش از این رفع (بدونِ PayVoucherId) برایِ سازگاریِ عقب‌رو از ۳-۰۱-۰۰۱.</summary>
public class ChangeChequeStatusTests
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
        public Task AddAsync(Cheque e, CancellationToken ct = default) { Items.Add(e); return Task.CompletedTask; }
        public Task<Cheque?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<Cheque>> GetByStatusAsync(int companyId, ChequeStatus status, CancellationToken ct = default) => Task.FromResult(new List<Cheque>());
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

    // U-ACCT-1.7: هارنس خالی — رفتارِ قدیمیِ fallbackِ ۱ را حفظ می‌کند (این تست‌ها به رفعِ
    // فیسکال‌یِر کاری ندارند).
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

    private static (ChangeChequeStatusCommandHandler H, FakeChequeRepo Cheques, FakeVoucherRepo Vouchers, FakeAccountRepo Accounts) NewSut()
    {
        var accounts = new FakeAccountRepo();
        accounts.AddAsync(Account.Create(1, "1-01-003", "بانک ملت", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "1-04-001", "اسناد دریافتنی", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "3-01-001", "حساب‌های پرداختنی", AccountLevel.Subsidiary, AccountNature.Credit, "بدهی")).Wait();
        accounts.AddAsync(Account.Create(1, "3-02-001", "اسناد پرداختنی - چک", AccountLevel.Subsidiary, AccountNature.Credit, "بدهی")).Wait();
        var cheques = new FakeChequeRepo();
        var vouchers = new FakeVoucherRepo();
        var fiscalYears = new FakeFiscalYearRepo();
        var h = new ChangeChequeStatusCommandHandler(new FakeUow(), new FakeUser(), cheques, accounts, vouchers, fiscalYears);
        return (h, cheques, vouchers, accounts);
    }

    [Fact]
    public async Task Clearing_New_Style_Paid_Cheque_Debits_NotesPayable()
    {
        var (h, cheques, vouchers, accounts) = NewSut();
        var cheque = Cheque.Create(1, 1, ChequeType.Paid, "999", "صادرات", 3_000_000, "1405/06/01");
        cheque.SetPayVoucher(555);   // شبیه‌سازیِ چکی که از مسیرِ نوِ RegisterChequeCommand رد شده
        await cheques.AddAsync(cheque);

        var res = await h.Handle(new ChangeChequeStatusCommand(cheque.Id, ChequeAction.Clear, "1405/06/01"), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Items.Single();
        Assert.True(v.IsBalanced());
        var notesPayable = accounts.Items.Single(a => a.Code == "3-02-001");
        Assert.Equal(3_000_000m, v.Items.Single(i => i.AccountId == notesPayable.Id).Debit);
    }

    [Fact]
    public async Task Clearing_Legacy_Paid_Cheque_Without_PayVoucherId_Debits_GeneralPayable()
    {
        var (h, cheques, vouchers, accounts) = NewSut();
        var cheque = Cheque.Create(1, 1, ChequeType.Paid, "998", "صادرات", 1_500_000, "1405/06/01");
        // PayVoucherId عمداً ست نمی‌شود — چکِ ثبت‌شده پیش از رفعِ U-ACCT-1.2.
        await cheques.AddAsync(cheque);

        var res = await h.Handle(new ChangeChequeStatusCommand(cheque.Id, ChequeAction.Clear, "1405/06/01"), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Items.Single();
        Assert.True(v.IsBalanced());
        var generalPayable = accounts.Items.Single(a => a.Code == "3-01-001");
        Assert.Equal(1_500_000m, v.Items.Single(i => i.AccountId == generalPayable.Id).Debit);
    }
}
