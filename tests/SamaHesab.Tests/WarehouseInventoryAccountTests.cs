using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-INV-ACCT-WH (backlog #7) — انتقالِ بینِ انبار (`TransferStockCommand`) حالا فقط وقتی
/// انبارهایِ مبدأ/مقصد حسابِ موجودیِ GLِ متفاوت دارند سند می‌زند. پیش‌فرض (بدونِ InventoryAccountId
/// روی هیچ انباری) دقیقاً رفتارِ قبل از این رفع را حفظ می‌کند — هیچ سندی زده نمی‌شود.</summary>
public class WarehouseInventoryAccountTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default) { Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default) { Items.AddRange(es); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<System.Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<System.Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> es) { }
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
        public Task<List<Account>> FindAsync(Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Account?> FindSingleAsync(Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
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
        public Voucher? Saved;
        public Task AddAsync(Voucher e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, 1); Saved = e; return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Voucher> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Saved);
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> FindAsync(Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> FindSingleAsync(Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult<Voucher?>(null);
        public Task<bool> AnyAsync(Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CountAsync(Expression<System.Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
        public void Update(Voucher e) { } public void Remove(Voucher e) { } public void RemoveRange(IEnumerable<Voucher> es) { }
        public Task<List<Voucher>> GetByDateRangeAsync(int c, int f, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int c, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> GetWithItemsAsync(int id, CancellationToken ct = default) => Task.FromResult(Saved);
        public Task<string> GetNextNumberAsync(int c, CancellationToken ct = default) => Task.FromResult("8001");
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
        public Task<List<StockItem>> FindAsync(Expression<System.Func<StockItem, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<StockItem?> FindSingleAsync(Expression<System.Func<StockItem, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<StockItem, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<StockItem, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(StockItem e) { }
        public void Remove(StockItem e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<StockItem> es) { }
        public Task<StockItem?> GetByProductAndWarehouseAsync(int productId, int warehouseId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.ProductId == productId && s.WarehouseId == warehouseId));
        public Task<List<StockItem>> GetByProductAsync(int productId, CancellationToken ct = default) => Task.FromResult(Items.Where(s => s.ProductId == productId).ToList());
        public Task<List<StockItem>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default) => Task.FromResult(Items.Where(s => s.WarehouseId == warehouseId).ToList());
        public Task<decimal> GetTotalQuantityAsync(int productId, CancellationToken ct = default) => Task.FromResult(Items.Where(s => s.ProductId == productId).Sum(s => s.Quantity));
    }

    private sealed class FakeWarehouseRepo : IWarehouseRepository
    {
        public readonly List<Warehouse> Items = new();
        public Task AddAsync(Warehouse e, CancellationToken ct = default) { Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Warehouse> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Warehouse?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(w => w.Id == id));
        public Task<List<Warehouse>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Warehouse>> FindAsync(Expression<System.Func<Warehouse, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Warehouse?> FindSingleAsync(Expression<System.Func<Warehouse, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<Warehouse, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<Warehouse, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(Warehouse e) { }
        public void Remove(Warehouse e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Warehouse> es) { }
        public Task<List<Warehouse>> GetByCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.Where(w => w.CompanyId == companyId).ToList());
        public Task<Warehouse?> GetDefaultAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(w => w.CompanyId == companyId));
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public IRepository<T> GetRepository<T>() where T : class => throw new System.NotImplementedException();
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public int? SalespersonPartyId => null;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    /// <summary>یک انبار با Id مشخص می‌سازد (با ست‌کردنِ Id از رویِ reflection، چون AddAsync سازنده اینجا AutoIncrement ندارد).</summary>
    private static Warehouse MakeWarehouse(int id, int companyId, int? inventoryAccountId)
    {
        var wh = Warehouse.Create(companyId, "W" + id, "انبار " + id);
        typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(wh, id);
        if (inventoryAccountId.HasValue) wh.SetInventoryAccount(inventoryAccountId);
        return wh;
    }

    private static (TransferStockCommandHandler Handler, FakeVoucherRepo Vouchers, FakeStockRepo Stock)
        Build(FakeWarehouseRepo warehouses, FakeAccountRepo accounts)
    {
        var stock = new FakeStockRepo();
        var source = StockItem.Create(1, 1);
        source.AddStock(100, 60_000);
        stock.AddAsync(source).Wait();

        var vouchers = new FakeVoucherRepo();
        var fiscalYears = new FakeRepo<FiscalYear>();
        fiscalYears.AddAsync(FiscalYear.Create(1, "۱۴۰۵", "1405/01/01", "1405/12/29")).Wait();

        var handler = new TransferStockCommandHandler(stock, new FakeRepo<StockTransaction>(), new FakeUow(),
            new FakeUser(), accounts, vouchers, warehouses, fiscalYears);

        return (handler, vouchers, stock);
    }

    [Fact]
    public async Task Transfer_WithDefaultWarehouses_PostsNoVoucher()
    {
        // هیچ انباری InventoryAccountId ندارد ⇒ هر دو به همان حسابِ مشترکِ ۱-۰۵-۰۰۱ resolve می‌شوند
        // ⇒ رفتارِ پیش‌فرض/قبل از این رفع باید دقیقاً حفظ شود: بدونِ سند.
        var accounts = new FakeAccountRepo();
        accounts.AddAsync(Account.Create(1, "1-05-001", "موجودی کالا", Domain.Enums.AccountLevel.Subsidiary, Domain.Enums.AccountNature.Debit, "دارایی")).Wait();

        var warehouses = new FakeWarehouseRepo();
        warehouses.Items.Add(MakeWarehouse(1, 1, null));
        warehouses.Items.Add(MakeWarehouse(2, 1, null));

        var (handler, vouchers, stock) = Build(warehouses, accounts);
        var cmd = new TransferStockCommand(FromWarehouseId: 1, ToWarehouseId: 2, ProductId: 1, Quantity: 10, Date: "1405/04/15", Description: "تست");

        var res = await handler.Handle(cmd, default);

        Assert.True(res.Succeeded);
        Assert.Null(vouchers.Saved);   // بدونِ حسابِ اختصاصی، هیچ سندی نباید زده شود
        Assert.Equal(90, stock.Items.First(s => s.WarehouseId == 1).Quantity);
        Assert.Equal(10, stock.Items.First(s => s.WarehouseId == 2).Quantity);
    }

    [Fact]
    public async Task Transfer_WithDifferentInventoryAccounts_PostsBalancedVoucher()
    {
        var accounts = new FakeAccountRepo();
        var shared = Account.Create(1, "1-05-001", "موجودی کالا (مشترک)", Domain.Enums.AccountLevel.Subsidiary, Domain.Enums.AccountNature.Debit, "دارایی");
        var acctA = Account.Create(1, "1-05-010", "موجودیِ انبارِ A", Domain.Enums.AccountLevel.Subsidiary, Domain.Enums.AccountNature.Debit, "دارایی");
        var acctB = Account.Create(1, "1-05-020", "موجودیِ انبارِ B", Domain.Enums.AccountLevel.Subsidiary, Domain.Enums.AccountNature.Debit, "دارایی");
        accounts.AddAsync(shared).Wait();
        accounts.AddAsync(acctA).Wait();
        accounts.AddAsync(acctB).Wait();

        var warehouses = new FakeWarehouseRepo();
        warehouses.Items.Add(MakeWarehouse(1, 1, acctA.Id));
        warehouses.Items.Add(MakeWarehouse(2, 1, acctB.Id));

        var (handler, vouchers, stock) = Build(warehouses, accounts);
        var cmd = new TransferStockCommand(FromWarehouseId: 1, ToWarehouseId: 2, ProductId: 1, Quantity: 10, Date: "1405/04/15", Description: "انتقالِ تستی");

        var res = await handler.Handle(cmd, default);

        Assert.True(res.Succeeded);
        Assert.NotNull(vouchers.Saved);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        Assert.Equal(600_000, v.Items.Sum(i => i.Debit));   // ۱۰ واحد × بهایِ میانگینِ ۶۰۰۰۰
        Assert.Equal(600_000, v.Items.Sum(i => i.Credit));
        Assert.Contains(v.Items, i => i.AccountId == acctB.Id && i.Debit == 600_000);   // مقصد بدهکار می‌شود
        Assert.Contains(v.Items, i => i.AccountId == acctA.Id && i.Credit == 600_000);  // مبدأ بستانکار می‌شود
    }

    [Fact]
    public async Task Transfer_WithOneWarehouseMissingChart_PostsNoVoucher()
    {
        // چارتِ حساب‌ها ناقص (حتی حسابِ مشترک هم تعریف نشده) ⇒ resolve به null ⇒ بی‌صدا رد شود (نه throw).
        var accounts = new FakeAccountRepo();
        var warehouses = new FakeWarehouseRepo();
        warehouses.Items.Add(MakeWarehouse(1, 1, null));
        warehouses.Items.Add(MakeWarehouse(2, 1, null));

        var (handler, vouchers, _) = Build(warehouses, accounts);
        var cmd = new TransferStockCommand(FromWarehouseId: 1, ToWarehouseId: 2, ProductId: 1, Quantity: 5, Date: "1405/04/15", Description: null);

        var res = await handler.Handle(cmd, default);

        Assert.True(res.Succeeded);
        Assert.Null(vouchers.Saved);
    }
}
