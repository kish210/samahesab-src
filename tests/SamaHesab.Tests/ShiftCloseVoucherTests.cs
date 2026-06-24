using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.POS.Application;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Modules.POS.Domain;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>T18 — سندِ حسابداریِ خودکارِ مغایرتِ نقدیِ بستنِ شیفت (Z-report).</summary>
public class ShiftCloseVoucherTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> e) { foreach (var x in e) Items.Remove(x); }
    }

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
        public string? Username => "cashier"; public string? FullName => "صندوق‌دار"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private sealed class FakeCalendar : IPersianCalendarService
    {
        public string ToPersianDate(DateTime date, string format = "yyyy/MM/dd") => "1403/03/15";
        public DateTime ToGregorianDate(string persianDate) => DateTime.Today;
        public string GetCurrentPersianDate() => "1403/03/15";
        public string GetCurrentPersianDateTime() => "1403/03/15 12:00";
        public string GetPersianMonthName(int month) => "خرداد";
        public int GetPersianYear(DateTime date) => 1403;
        public int GetPersianMonth(DateTime date) => 3;
        public int GetPersianDay(DateTime date) => 15;
        public string FormatCurrency(decimal amount, bool showToman = false) => amount.ToString("N0");
        public string NumberToWords(decimal number) => "";
    }

    private static (CloseShiftCommandHandler h, FakeVoucherRepo v, FakeRepo<CashShift> shifts) NewSut(bool withFiscalYear = true)
    {
        var shifts = new FakeRepo<CashShift>();
        var acc = new FakeAccountRepo();
        acc.AddAsync(Account.Create(1, "1-01-001", "صندوق", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        acc.AddAsync(Account.Create(1, "8-11-001", "کسری صندوق", AccountLevel.Subsidiary, AccountNature.Debit, "هزینه")).Wait();
        acc.AddAsync(Account.Create(1, "6-03-001", "اضافات صندوق", AccountLevel.Subsidiary, AccountNature.Credit, "درآمد")).Wait();
        var vch = new FakeVoucherRepo();
        var fy = new FakeRepo<FiscalYear>();
        if (withFiscalYear) fy.AddAsync(FiscalYear.Create(1, "1403", "1403/01/01", "1403/12/29")).Wait();
        var h = new CloseShiftCommandHandler(shifts, new FakeUow(), new FakeUser(), acc, vch, fy, new FakeCalendar());
        return (h, vch, shifts);
    }

    private static async Task OpenShiftAsync(FakeRepo<CashShift> shifts, decimal openingFloat, decimal cashSales)
    {
        var s = CashShift.Open(1, 1, 7, openingFloat);
        if (cashSales > 0) s.RecordSale(cashSales, isCash: true);
        await shifts.AddAsync(s);
    }

    [Fact]
    public async Task Shortage_Posts_Voucher_Debit_Shortage_Credit_Cash()
    {
        var (h, vch, shifts) = NewSut();
        await OpenShiftAsync(shifts, openingFloat: 1_000_000, cashSales: 500_000); // موردانتظار = ۱٬۵۰۰٬۰۰۰

        var res = await h.Handle(new CloseShiftCommand(CountedCash: 1_450_000), default); // کسری ۵۰٬۰۰۰

        Assert.True(res.Succeeded);
        Assert.Equal(-50_000, res.Value!.Variance);
        var v = Assert.Single(vch.Items);
        Assert.Equal(VoucherStatus.Posted, v.Status);
        Assert.True(v.IsBalanced());
        Assert.Equal(50_000, v.TotalDebit);
        // بدهکار روی حسابِ کسری، بستانکار روی صندوق
        var debit = v.Items.Single(i => i.Debit > 0);
        var credit = v.Items.Single(i => i.Credit > 0);
        Assert.Equal(50_000, debit.Debit);
        Assert.Equal(50_000, credit.Credit);
        Assert.Equal(res.Value.VarianceVoucherId, v.Id);
    }

    [Fact]
    public async Task Surplus_Posts_Voucher_Debit_Cash_Credit_Surplus()
    {
        var (h, vch, shifts) = NewSut();
        await OpenShiftAsync(shifts, 1_000_000, 500_000);

        var res = await h.Handle(new CloseShiftCommand(1_600_000), default); // اضافه ۱۰۰٬۰۰۰

        Assert.True(res.Succeeded);
        Assert.Equal(100_000, res.Value!.Variance);
        var v = Assert.Single(vch.Items);
        Assert.True(v.IsBalanced());
        Assert.Equal(100_000, v.TotalDebit);
    }

    [Fact]
    public async Task Zero_Variance_Creates_No_Voucher()
    {
        var (h, vch, shifts) = NewSut();
        await OpenShiftAsync(shifts, 1_000_000, 500_000);

        var res = await h.Handle(new CloseShiftCommand(1_500_000), default); // دقیقاً موردانتظار

        Assert.True(res.Succeeded);
        Assert.Equal(0, res.Value!.Variance);
        Assert.Empty(vch.Items);
        Assert.Null(res.Value.VarianceVoucherId);
    }

    [Fact]
    public async Task No_Active_Fiscal_Year_Closes_Shift_Without_Voucher()
    {
        var (h, vch, shifts) = NewSut(withFiscalYear: false);
        await OpenShiftAsync(shifts, 1_000_000, 500_000);

        var res = await h.Handle(new CloseShiftCommand(1_400_000), default); // کسری، ولی سال مالی نیست

        Assert.True(res.Succeeded);          // بستنِ شیفت نباید بلاک شود
        Assert.Empty(vch.Items);             // سندی ساخته نشد
        Assert.Null(res.Value!.VarianceVoucherId);
    }
}
