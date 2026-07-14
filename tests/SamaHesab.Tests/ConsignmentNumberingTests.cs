using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-CONSIGN-1 — با تستِ زندهٔ رویِ DBِ واقعی بازتولید شد: فاکتورِ Consignment پیشوندِ
/// "F" را با Sale مشترک می‌گرفت، پس هر دو اولین شمارهٔ یکسان ("F000001") می‌گرفتند — دو فاکتورِ
/// کاملاً متفاوت با شمارهٔ رسمیِ تکراری (نقصِ قانونی، طبقِ کامنتِ خودِ کد).</summary>
public class ConsignmentNumberingTests
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

    private sealed class FakeCalendar : IPersianCalendarService
    {
        public string ToPersianDate(System.DateTime date, string format = "yyyy/MM/dd") => "";
        public System.DateTime ToGregorianDate(string persianDate) => System.DateTime.Now;
        public string GetCurrentPersianDate() => "";
        public string GetCurrentPersianDateTime() => "";
        public string GetPersianMonthName(int month) => "";
        public int GetPersianYear(System.DateTime date) => 1405;
        public int GetPersianMonth(System.DateTime date) => 1;
        public int GetPersianDay(System.DateTime date) => 1;
        public string FormatCurrency(decimal amount, bool showToman = false) => "";
        public string NumberToWords(decimal number) => "";
    }

    private sealed class FakeMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => throw new System.NotImplementedException("این تست مسیرِ بچ/سریال را صدا نمی‌زند.");
        public Task<object?> Send(object request, CancellationToken ct = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => null!;
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => null!;
        public Task Publish(object n, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    private static (CreateSalesInvoiceCommandHandler Handler, FakeRepo<SalesInvoice> Invoices) Build()
    {
        var accounts = new FakeAccountRepo();
        accounts.AddAsync(Account.Create(1, "1-03-001", "دریافتنی", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "6-01-001", "درآمد فروش", AccountLevel.Subsidiary, AccountNature.Credit, "درآمد")).Wait();
        accounts.AddAsync(Account.Create(1, "1-01-001", "صندوق", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();

        var products = new FakeProductRepo();
        products.AddAsync(Product.Create(1, "P1", "کالایِ آزمایشی", 1, 100_000, 60_000)).Wait();

        var stock = new FakeStockRepo();
        var stockItem = StockItem.Create(1, 1);
        stockItem.AddStock(100, 60_000);
        stock.AddAsync(stockItem).Wait();

        var fiscalYears = new FakeRepo<FiscalYear>();
        fiscalYears.AddAsync(FiscalYear.Create(1, "۱۴۰۵", "1405/01/01", "1405/12/29")).Wait();

        var invoices = new FakeRepo<SalesInvoice>();
        var handler = new CreateSalesInvoiceCommandHandler(
            invoices, new FakeUow(), new FakeUser(), new FakeCalendar(),
            stock, products, accounts, new FakeVoucherRepo(),
            new FakeRepo<Domain.Entities.Inventory.StockTransaction>(), new FakeRepo<Party>(),
            fiscalYears, new FakeRepo<BankAccount>(), new FakeMediator());

        return (handler, invoices);
    }

    private static CreateSalesInvoiceCommand MakeCommand(InvoiceType type) => new(
        BranchId: 1, FiscalYearId: 1, InvoiceDate: "1405/04/15", CustomerId: 1, WarehouseId: 1,
        InvoiceType: type, PriceLevel: "خرده", SalesRepId: null, DueDate: null, Description: null,
        Shipping: 0, OtherCosts: 0,
        Items: new List<SalesInvoiceItemDto> { new(1, Quantity: 1, UnitPrice: 100_000, DiscountPct: 0, TaxPct: 0, Description: null, BatchId: null, SerialId: null) },
        PaidAmount: 0, PaymentMethod: "نسیه");

    [Fact]
    public async Task Consignment_Invoice_Number_Does_Not_Collide_With_Sale_Invoice_Number()
    {
        var (handler, invoices) = Build();

        var saleRes = await handler.Handle(MakeCommand(InvoiceType.Sale), default);
        var consignRes = await handler.Handle(MakeCommand(InvoiceType.Consignment), default);

        Assert.True(saleRes.Succeeded, saleRes.ErrorMessage);
        Assert.True(consignRes.Succeeded, consignRes.ErrorMessage);

        var saleNumber = invoices.Items.Single(i => i.Id == saleRes.Value).InvoiceNumber;
        var consignNumber = invoices.Items.Single(i => i.Id == consignRes.Value).InvoiceNumber;

        Assert.NotEqual(saleNumber, consignNumber);
        Assert.StartsWith("HV", consignNumber);
        Assert.StartsWith("F", saleNumber);
    }
}
