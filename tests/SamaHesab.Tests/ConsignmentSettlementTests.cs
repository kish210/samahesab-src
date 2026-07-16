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

/// <summary>U-CONSIGN-SETTLE — تسویهٔ کنسینمنت: خروج از «کالای امانی نزدِ دیگران» (۱-۰۵-۰۰۳)
/// + سندِ واقعیِ درآمد/COGS/دریافتنی، دقیقاً به همان بهایی که سندِ اصلیِ کنسینمنت ثبت کرده بود.</summary>
public class ConsignmentSettlementTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        private static int GetId(T e) => (int)(typeof(T).GetProperty("Id")!.GetValue(e) ?? 0);
        private static void SetId(T e, int value) => typeof(T).GetProperty("Id")!.SetValue(e, value);
        public Task AddAsync(T e, CancellationToken ct = default)
        { SetId(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default)
        { foreach (var e in es) { SetId(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(e => GetId(e) == id));
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
        public readonly List<Voucher> Items = new();
        private int _seq;
        public Task AddAsync(Voucher e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Voucher> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id));
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Voucher>> FindAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Voucher?> FindSingleAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(Voucher e) { } public void Remove(Voucher e) { } public void RemoveRange(IEnumerable<Voucher> es) { }
        public Task<List<Voucher>> GetByDateRangeAsync(int c, int f, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int c, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> GetWithItemsAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(v => v.Id == id));
        public Task<string> GetNextNumberAsync(int c, CancellationToken ct = default) => Task.FromResult((Items.Count + 4001).ToString());
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

    private static (CreateSalesInvoiceCommandHandler CreateHandler, SettleConsignmentCommandHandler SettleHandler,
        FakeRepo<SalesInvoice> Invoices, FakeAccountRepo Accounts, FakeVoucherRepo Vouchers, FakeRepo<Party> Customers)
        Build()
    {
        var accounts = new FakeAccountRepo();
        accounts.AddAsync(Account.Create(1, "1-03-001", "دریافتنی", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "6-01-001", "درآمد فروش", AccountLevel.Subsidiary, AccountNature.Credit, "درآمد")).Wait();
        accounts.AddAsync(Account.Create(1, "1-01-001", "صندوق", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "1-05-001", "موجودی کالا", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "1-05-003", "کالای امانی نزدِ دیگران", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        accounts.AddAsync(Account.Create(1, "7-01-001", "بهای تمام‌شده", AccountLevel.Subsidiary, AccountNature.Debit, "هزینه")).Wait();
        accounts.AddAsync(Account.Create(1, "3-04-001", "مالیات بر ارزش افزوده", AccountLevel.Subsidiary, AccountNature.Credit, "بدهی")).Wait();

        var products = new FakeProductRepo();
        products.AddAsync(Product.Create(1, "P1", "کالایِ آزمایشی", 1, 100_000, 60_000)).Wait();

        var stock = new FakeStockRepo();
        var stockItem = StockItem.Create(1, 1);
        stockItem.AddStock(100, 60_000);
        stock.AddAsync(stockItem).Wait();

        var fiscalYears = new FakeRepo<FiscalYear>();
        fiscalYears.AddAsync(FiscalYear.Create(1, "۱۴۰۵", "1405/01/01", "1405/12/29")).Wait();

        var customers = new FakeRepo<Party>();
        customers.AddAsync(Party.Create(1, "M1001", "حقیقی", "مشتری", "آزمایشی", isCustomer: true)).Wait();

        var invoices = new FakeRepo<SalesInvoice>();
        var vouchers = new FakeVoucherRepo();
        var createHandler = new CreateSalesInvoiceCommandHandler(
            invoices, new FakeUow(), new FakeUser(), new FakeCalendar(),
            stock, products, accounts, vouchers,
            new FakeRepo<Domain.Entities.Inventory.StockTransaction>(), customers,
            fiscalYears, new FakeRepo<BankAccount>(), new FakeMediator());

        var settleHandler = new SettleConsignmentCommandHandler(
            invoices, vouchers, accounts, customers, new FakeRepo<BankAccount>(), new FakeUow(), new FakeUser());

        return (createHandler, settleHandler, invoices, accounts, vouchers, customers);
    }

    private static CreateSalesInvoiceCommand MakeConsignmentCommand(decimal qty = 5, decimal unitPrice = 100_000, decimal taxPct = 9) => new(
        BranchId: 1, FiscalYearId: 1, InvoiceDate: "1405/04/15", CustomerId: 1, WarehouseId: 1,
        InvoiceType: InvoiceType.Consignment, PriceLevel: "خرده", SalesRepId: null, DueDate: null, Description: null,
        Shipping: 0, OtherCosts: 0,
        Items: new List<SalesInvoiceItemDto> { new(1, Quantity: qty, UnitPrice: unitPrice, DiscountPct: 0, TaxPct: taxPct, Description: null, BatchId: null, SerialId: null) },
        PaidAmount: 0, PaymentMethod: "نسیه");

    [Fact]
    public async Task Settle_Posts_Balanced_Voucher_With_Revenue_Cogs_And_Consignment_OutClear()
    {
        var (createHandler, settleHandler, invoices, accounts, vouchers, _) = Build();

        var createRes = await createHandler.Handle(MakeConsignmentCommand(), default);
        Assert.True(createRes.Succeeded, createRes.ErrorMessage);
        var invoiceId = createRes.Value;

        var settleRes = await settleHandler.Handle(new SettleConsignmentCommand(invoiceId, "1405/05/01", PaidAmount: 0, PaymentMethod: "نسیه"), default);
        Assert.True(settleRes.Succeeded, settleRes.ErrorMessage);

        var settlementVoucher = vouchers.Items.Single(v => v.Id == settleRes.Value);
        Assert.True(settlementVoucher.IsBalanced());

        var sales = accounts.Items.Single(a => a.Code == "6-01-001");
        var cogs = accounts.Items.Single(a => a.Code == "7-01-001");
        var consignmentOut = accounts.Items.Single(a => a.Code == "1-05-003");
        var receivable = accounts.Items.Single(a => a.Code == "1-03-001");

        // ۵ واحد × ۱۰۰٬۰۰۰ = ۵۰۰٬۰۰۰ درآمد (بدونِ مالیات)؛ بهایِ تمام‌شده = ۵ × ۶۰٬۰۰۰ = ۳۰۰٬۰۰۰.
        Assert.Equal(500_000m, settlementVoucher.Items.Single(i => i.AccountId == sales.Id).Credit);
        Assert.Equal(300_000m, settlementVoucher.Items.Single(i => i.AccountId == cogs.Id).Debit);
        Assert.Equal(300_000m, settlementVoucher.Items.Single(i => i.AccountId == consignmentOut.Id).Credit);
        Assert.True(settlementVoucher.Items.Any(i => i.AccountId == receivable.Id && i.Debit > 0));

        var invoice = invoices.Items.Single(i => i.Id == invoiceId);
        Assert.Equal(settleRes.Value, invoice.SettledVoucherId);
    }

    [Fact]
    public async Task Settle_Fails_When_Already_Settled()
    {
        var (createHandler, settleHandler, _, _, _, _) = Build();
        var createRes = await createHandler.Handle(MakeConsignmentCommand(), default);
        var invoiceId = createRes.Value;

        var first = await settleHandler.Handle(new SettleConsignmentCommand(invoiceId, "1405/05/01"), default);
        Assert.True(first.Succeeded, first.ErrorMessage);

        var second = await settleHandler.Handle(new SettleConsignmentCommand(invoiceId, "1405/05/02"), default);
        Assert.False(second.Succeeded);
    }

    [Fact]
    public async Task Settle_Fails_When_Invoice_Is_Not_Consignment_Type()
    {
        var (createHandler, settleHandler, _, _, _, _) = Build();
        var saleCmd = MakeConsignmentCommand() with { InvoiceType = InvoiceType.Sale };
        var createRes = await createHandler.Handle(saleCmd, default);

        var settleRes = await settleHandler.Handle(new SettleConsignmentCommand(createRes.Value, "1405/05/01"), default);
        Assert.False(settleRes.Succeeded);
    }

    [Fact]
    public async Task GetOpenConsignmentsQuery_Excludes_Settled_Consignments()
    {
        var (createHandler, settleHandler, invoices, _, _, customers) = Build();
        var c1 = await createHandler.Handle(MakeConsignmentCommand(), default);
        var c2 = await createHandler.Handle(MakeConsignmentCommand(qty: 2), default);
        await settleHandler.Handle(new SettleConsignmentCommand(c1.Value, "1405/05/01"), default);

        var handler = new GetOpenConsignmentsQueryHandler(invoices, customers, new FakeUser());
        var open = await handler.Handle(new GetOpenConsignmentsQuery(), default);

        Assert.Single(open);
        Assert.Equal(c2.Value, open[0].InvoiceId);
    }
}
