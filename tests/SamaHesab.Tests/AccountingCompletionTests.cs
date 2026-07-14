using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Purchase.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-ACCT-1.1 — پیش‌تر مالیاتِ خرید بی‌سروصدا داخلِ سندِ موجودی folded می‌شد (کلِ کالا+مالیات+
/// حمل یک‌جا Dr موجودی)، درحالی‌که AddStock همیشه فقط UnitPriceِ خامِ بدونِ‌مالیات را برایِ ارزش‌گذاریِ
/// Kardex به‌کار می‌برد — یعنی ماندهٔ GLِ موجودی همیشه از ارزشِ واقعیِ Kardex جلوتر بود. حالا مالیات
/// جدا و به‌عنوانِ دارایی/طلبِ قابلِ‌کسر (۱-۰۶-۰۰۱) ثبت می‌شود.</summary>
public class AccountingCompletionTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        // U-ACCT-1.1: برخلافِ اکثرِ موجودیت‌ها، StockTransaction/AuditLog از BaseEntity ارث نمی‌برند
        // و Idِ خودشان از نوعِ long است — پس باید Idِ نوعِ خودِ T (نه لزوماً BaseEntity) پیدا و مقدارِ
        // عددی به همان نوع (int/long) تبدیل شود.
        private static void SetId(T e, int value)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop != null) prop.SetValue(e, System.Convert.ChangeType(value, prop.PropertyType));
        }
        public Task AddAsync(T e, CancellationToken ct = default)
        { SetId(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
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
        public Task<string> GetNextNumberAsync(int c, CancellationToken ct = default) => Task.FromResult("4001");
    }

    private sealed class FakeStockRepo : IStockItemRepository
    {
        public readonly List<StockItem> Items = new();
        private int _seq;
        public Task AddAsync(StockItem e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<StockItem> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StockItem?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<StockItem>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<StockItem>> FindAsync(Expression<Func<StockItem, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<StockItem?> FindSingleAsync(Expression<Func<StockItem, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<StockItem, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<StockItem, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(StockItem e) { }
        public void Remove(StockItem e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<StockItem> es) { }
        public Task<StockItem?> GetByProductAndWarehouseAsync(int productId, int warehouseId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.ProductId == productId && s.WarehouseId == warehouseId));
        public Task<List<StockItem>> GetByProductAsync(int productId, CancellationToken ct = default) => Task.FromResult(Items.Where(s => s.ProductId == productId).ToList());
        public Task<List<StockItem>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default) => Task.FromResult(Items.Where(s => s.WarehouseId == warehouseId).ToList());
        public Task<decimal> GetTotalQuantityAsync(int productId, CancellationToken ct = default) => Task.FromResult(Items.Where(s => s.ProductId == productId).Sum(s => s.Quantity));
    }

    private sealed class FakeProductRepo : IProductRepository
    {
        public readonly List<Product> Items = new();
        private int _seq;
        public Task AddAsync(Product e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Product> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
        public Task<List<Product>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Product>> FindAsync(Expression<Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Product?> FindSingleAsync(Expression<Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(Product e) { }
        public void Remove(Product e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Product> es) { }
        public Task<Product?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.CompanyId == companyId && p.Code == code));
        public Task<Product?> GetByBarcodeAsync(int companyId, string barcode, CancellationToken ct = default) => Task.FromResult<Product?>(null);
        public Task<List<Product>> SearchAsync(int companyId, string searchText, CancellationToken ct = default) => Task.FromResult(new List<Product>());
        public Task<List<Product>> GetByGroupAsync(int groupId, CancellationToken ct = default) => Task.FromResult(new List<Product>());
        public Task<List<Product>> GetLowStockAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Product>());
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

    private sealed class FakeMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => throw new System.NotImplementedException("این تست خطِ بچ/سریال را صدا نمی‌زند.");
        public Task<object?> Send(object request, CancellationToken ct = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => null!;
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => null!;
        public Task Publish(object n, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private static (CreatePurchaseInvoiceCommandHandler Handler, FakeAccountRepo Accounts, FakeVoucherRepo Vouchers)
        Build(bool withVatAccount)
    {
        var accounts = new FakeAccountRepo();
        accounts.AddAsync(Account.Create(1, "1-05-001", "موجودی کالا", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "3-01-001", "پرداختنی", AccountLevel.Subsidiary, AccountNature.Credit, "بدهی")).Wait();
        if (withVatAccount)
            accounts.AddAsync(Account.Create(1, "1-06-001", "مالیاتِ خریدِ قابلِ‌کسر", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();

        var products = new FakeProductRepo();
        products.AddAsync(Product.Create(1, "P1", "کالایِ آزمایشی", 1, 150, 100)).Wait();

        var fiscalYears = new FakeRepo<FiscalYear>();
        fiscalYears.AddAsync(FiscalYear.Create(1, "۱۴۰۵", "1405/01/01", "1405/12/29")).Wait();

        var vouchers = new FakeVoucherRepo();
        var handler = new CreatePurchaseInvoiceCommandHandler(
            new FakeUow(), new FakeUser(), new FakeStockRepo(), products, accounts,
            vouchers, new FakeRepo<PurchaseInvoice>(), new FakeRepo<Domain.Entities.Inventory.StockTransaction>(),
            fiscalYears, new FakeRepo<Party>(), new FakeMediator());

        return (handler, accounts, vouchers);
    }

    [Fact]
    public async Task Purchase_Splits_Tax_Into_Dedicated_Deductible_Account()
    {
        var (handler, accounts, vouchers) = Build(withVatAccount: true);
        var cmd = new CreatePurchaseInvoiceCommand(
            BranchId: 1, FiscalYearId: 1, InvoiceDate: "1405/04/15", SupplierId: 1, WarehouseId: 1,
            InvoiceType: "خرید", OrderId: null, DueDate: null, Description: null,
            Shipping: 50_000, OtherCosts: 0,
            Items: new List<PurchaseInvoiceItemDto> { new(1, Quantity: 10, UnitPrice: 100_000, DiscountPct: 0, TaxPct: 9, Description: null, BatchId: null, BatchNumber: null, ProductionDate: null, ExpiryDate: null) },
            PaidAmount: 0);

        var res = await handler.Handle(cmd, default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        var inventory = accounts.Items.Single(a => a.Code == "1-05-001");
        var vat = accounts.Items.Single(a => a.Code == "1-06-001");
        // goods=1,000,000 + shipping=50,000 = 1,050,000 (خالص از مالیات)
        Assert.Equal(1_050_000m, v.Items.Where(i => i.AccountId == inventory.Id).Sum(i => i.Debit));
        // مالیات = 1,000,000 * 9% = 90,000 جدا رفته به حسابِ قابلِ‌کسر
        Assert.Equal(90_000m, v.Items.Where(i => i.AccountId == vat.Id).Sum(i => i.Debit));
        Assert.Equal(1_140_000m, v.Items.Sum(i => i.Debit));
    }

    [Fact]
    public async Task Purchase_Falls_Back_To_Folding_Tax_Into_Inventory_When_No_Dedicated_Account()
    {
        var (handler, accounts, vouchers) = Build(withVatAccount: false);
        var cmd = new CreatePurchaseInvoiceCommand(
            BranchId: 1, FiscalYearId: 1, InvoiceDate: "1405/04/15", SupplierId: 1, WarehouseId: 1,
            InvoiceType: "خرید", OrderId: null, DueDate: null, Description: null,
            Shipping: 0, OtherCosts: 0,
            Items: new List<PurchaseInvoiceItemDto> { new(1, Quantity: 10, UnitPrice: 100_000, DiscountPct: 0, TaxPct: 9, Description: null, BatchId: null, BatchNumber: null, ProductionDate: null, ExpiryDate: null) },
            PaidAmount: 0);

        var res = await handler.Handle(cmd, default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        var inventory = accounts.Items.Single(a => a.Code == "1-05-001");
        // بدونِ حسابِ اختصاصی: کلِ مبلغ (شاملِ مالیات) مثلِ رفتارِ قدیمی رویِ موجودی می‌رود.
        Assert.Equal(1_090_000m, v.Items.Where(i => i.AccountId == inventory.Id).Sum(i => i.Debit));
    }
}
