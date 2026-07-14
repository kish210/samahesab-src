using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Treasury.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-ACCT-1.3 — دریافت/پرداخت حالا می‌توانند یک فاکتورِ مشخص را هدف بگیرند، و مازادِ
/// بیشتر از مجموعِ ماندهٔ فاکتورهایِ باز دیگر بی‌سروصدا دور ریخته نمی‌شود (به پیش‌دریافت/
/// پیش‌پرداخت ثبت می‌شود).</summary>
public class PaymentAllocationCommandTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        private static void SetId(T e, int value)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop != null) prop.SetValue(e, System.Convert.ChangeType(value, prop.PropertyType));
        }
        public Task AddAsync(T e, CancellationToken ct = default) { SetId(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default)
        { foreach (var e in es) { SetId(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
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

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public readonly List<Account> Items = new();
        private int _seq;
        public Task AddAsync(Account e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Account> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
        public Task<List<Account>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> FindAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Account?> FindSingleAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(Account e) { }
        public void Remove(Account e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Account> es) { }
        public Task<List<Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.Where(a => a.CompanyId == companyId).ToList());
        public Task<Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(a => a.CompanyId == companyId && a.Code == code));
        public Task<List<Account>> GetChildrenAsync(int parentId, CancellationToken ct = default) => Task.FromResult(Items.Where(a => a.ParentId == parentId).ToList());
        public Task<List<Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.Where(a => a.CompanyId == companyId && a.IsLeaf).ToList());
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default) => Task.FromResult(0m);
    }

    private sealed class FakeVoucherRepo : IVoucherRepository
    {
        public Voucher? Saved; private int _seq;
        public Task AddAsync(Voucher e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Saved = e; return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Voucher> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Saved);
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> FindAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> FindSingleAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult<Voucher?>(null);
        public Task<bool> AnyAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CountAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
        public void Update(Voucher e) { } public void Remove(Voucher e) { } public void RemoveRange(IEnumerable<Voucher> es) { }
        public Task<List<Voucher>> GetByDateRangeAsync(int c, int f, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int c, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> GetWithItemsAsync(int id, CancellationToken ct = default) => Task.FromResult(Saved);
        public Task<string> GetNextNumberAsync(int c, CancellationToken ct = default) => Task.FromResult("9001");
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
        public int? SalespersonPartyId => null;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static SalesInvoice MakeSaleInvoice(int customerId, string number, decimal price)
    {
        var inv = SalesInvoice.Create(1, 1, 1, number, "1405/04/01", customerId, 1);
        inv.AddItem(SalesInvoiceItem.Create(0, 1, 1, 1, price, 0, 0, null, null, null));
        inv.Post(1, 0);   // تا فیلترِ Status==Posted در CreateReceiptCommand این فاکتور را ببیند
        return inv;
    }

    [Fact]
    public async Task Receipt_Targets_Specific_Invoice_Before_Fifo()
    {
        var accounts = new FakeAccountRepo();
        accounts.AddAsync(Account.Create(1, "1-01-001", "صندوق", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "1-03-001", "دریافتنی", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();

        var invoices = new FakeRepo<SalesInvoice>();
        var older = MakeSaleInvoice(1, "S1", 1_000_000);   // تاریخِ قدیمی‌تر → FIFO این را اول می‌گرفت
        var newer = MakeSaleInvoice(1, "S2", 1_000_000);
        invoices.AddAsync(older).Wait();
        invoices.AddAsync(newer).Wait();

        var handler = new CreateReceiptCommandHandler(new FakeUow(), new FakeUser(), accounts,
            new FakeVoucherRepo(), new FakeRepo<Party>(), invoices, new FakeRepo<FiscalYear>(), new FakeRepo<BankAccount>());

        // هدف‌گیریِ صریحِ فاکتورِ دوم، با اینکه FIFO اول‌ همان اولی را می‌گرفت.
        var res = await handler.Handle(new CreateReceiptCommand(
            1, 1, "1405/04/15", CustomerId: 1, Amount: 1_000_000, InvoiceId: newer.Id), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        Assert.Equal(0m, newer.RemainAmount);       // فاکتورِ هدف کامل تسویه شد
        Assert.Equal(1_000_000m, older.RemainAmount); // فاکتورِ قدیمی‌تر دست‌نخورده ماند
    }

    [Fact]
    public async Task Receipt_Surplus_Beyond_Open_Invoices_Posts_To_Advance_Account_Not_Discarded()
    {
        var accounts = new FakeAccountRepo();
        accounts.AddAsync(Account.Create(1, "1-01-001", "صندوق", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "1-03-001", "دریافتنی", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "3-03-001", "پیش‌دریافت", AccountLevel.Subsidiary, AccountNature.Credit, "بدهی")).Wait();

        var invoices = new FakeRepo<SalesInvoice>();
        var inv = MakeSaleInvoice(1, "S1", 600_000);
        invoices.AddAsync(inv).Wait();

        var vouchers = new FakeVoucherRepo();
        var handler = new CreateReceiptCommandHandler(new FakeUow(), new FakeUser(), accounts,
            vouchers, new FakeRepo<Party>(), invoices, new FakeRepo<FiscalYear>(), new FakeRepo<BankAccount>());

        var res = await handler.Handle(new CreateReceiptCommand(
            1, 1, "1405/04/15", CustomerId: 1, Amount: 1_000_000), default);   // ۴۰۰,۰۰۰ بیش از ماندهٔ فاکتور

        Assert.True(res.Succeeded, res.ErrorMessage);
        Assert.Equal(0m, inv.RemainAmount);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        var advance = accounts.Items.Single(a => a.Code == "3-03-001");
        var receivable = accounts.Items.Single(a => a.Code == "1-03-001");
        Assert.Equal(400_000m, v.Items.Where(i => i.AccountId == advance.Id).Sum(i => i.Credit));
        Assert.Equal(600_000m, v.Items.Where(i => i.AccountId == receivable.Id).Sum(i => i.Credit));
        Assert.Equal(1_000_000m, v.Items.Sum(i => i.Debit));   // کلِ مبلغ همچنان یک‌جا از صندوق بدهکار
    }

    [Fact]
    public async Task Payment_Surplus_Beyond_Open_Invoices_Posts_To_Advance_Account()
    {
        var accounts = new FakeAccountRepo();
        accounts.AddAsync(Account.Create(1, "1-01-001", "صندوق", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "3-01-001", "پرداختنی", AccountLevel.Subsidiary, AccountNature.Credit, "بدهی")).Wait();
        accounts.AddAsync(Account.Create(1, "1-06-002", "پیش‌پرداخت به تأمین‌کننده", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();

        var invoices = new FakeRepo<PurchaseInvoice>();
        var inv = PurchaseInvoice.Create(1, 1, 1, "P1", "1405/04/01", 1, 1);
        inv.AddItem(PurchaseInvoiceItem.Create(0, 1, 1, 1, 300_000, 0, 0));
        inv.Post(0);   // تا فیلترِ StatusCode=="قطعی" در CreatePaymentCommand این فاکتور را ببیند
        invoices.AddAsync(inv).Wait();

        var vouchers = new FakeVoucherRepo();
        var handler = new CreatePaymentCommandHandler(new FakeUow(), new FakeUser(), accounts,
            vouchers, new FakeRepo<Party>(), invoices, new FakeRepo<FiscalYear>(), new FakeRepo<BankAccount>());

        var res = await handler.Handle(new CreatePaymentCommand(
            1, 1, "1405/04/15", SupplierId: 1, Amount: 500_000), default);   // ۲۰۰,۰۰۰ بیش از ماندهٔ فاکتور

        Assert.True(res.Succeeded, res.ErrorMessage);
        Assert.Equal(0m, inv.RemainAmount);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        var advance = accounts.Items.Single(a => a.Code == "1-06-002");
        Assert.Equal(200_000m, v.Items.Where(i => i.AccountId == advance.Id).Sum(i => i.Debit));
    }

    [Fact]
    public async Task Receipt_With_BankAccountId_Debits_Its_Linked_GL_Account_Not_Default_Bank()
    {
        // U-ACCT-1.4: بدونِ BankAccountId، «بانک» همیشه به بانکِ پیش‌فرضِ تک‌بانکی (1-01-003) می‌رفت.
        var accounts = new FakeAccountRepo();
        var mellat = await AddAcc(accounts, "1-01-003", "بانک ملت (پیش‌فرض)");
        var saderat = await AddAcc(accounts, "1-01-006", "بانک صادرات (دومین حساب)");
        await AddAcc(accounts, "1-03-001", "دریافتنی");

        var bankAccounts = new FakeRepo<BankAccount>();
        var secondBank = BankAccount.Create(1, saderat.Id, "صادرات", "1234567890");
        await bankAccounts.AddAsync(secondBank);

        var vouchers = new FakeVoucherRepo();
        var handler = new CreateReceiptCommandHandler(new FakeUow(), new FakeUser(), accounts,
            vouchers, new FakeRepo<Party>(), new FakeRepo<SalesInvoice>(), new FakeRepo<FiscalYear>(), bankAccounts);

        var res = await handler.Handle(new CreateReceiptCommand(
            1, 1, "1405/04/15", CustomerId: 1, Amount: 500_000, PaymentMethod: "بانک", BankAccountId: secondBank.Id), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        Assert.Equal(500_000m, v.Items.Where(i => i.AccountId == saderat.Id).Sum(i => i.Debit));
        Assert.Equal(0m, v.Items.Where(i => i.AccountId == mellat.Id).Sum(i => i.Debit));
    }

    private static async Task<Account> AddAcc(FakeAccountRepo repo, string code, string name)
    {
        var acc = Account.Create(1, code, name, AccountLevel.Subsidiary, AccountNature.Debit, "دارایی");
        await repo.AddAsync(acc);
        return acc;
    }
}
