using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Domain.Enums;
using SamaHesab.Infrastructure.Data;

namespace SamaHesab.Infrastructure.Repositories;

// Accounting Repositories
public class VoucherRepository : GenericRepository<Voucher>, IVoucherRepository
{
    public VoucherRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<Voucher>> GetByDateRangeAsync(
        int companyId, int fiscalYearId, string fromDate, string toDate, CancellationToken ct = default)
    {
        return await DbSet
            .Include(v => v.Items)   // لازم برای جمعِ بدهکار/بستانکار و تشخیص توازن در لیستِ اسناد
            .Where(v => v.CompanyId == companyId && v.FiscalYearId == fiscalYearId
                && string.Compare(v.VoucherDate, fromDate) >= 0
                && string.Compare(v.VoucherDate, toDate) <= 0)
            .ToListAsync(ct);
    }

    public async Task<List<Voucher>> GetByDateRangeWithItemsAsync(
        int companyId, string fromDate, string toDate, CancellationToken ct = default)
    {
        return await DbSet.Include(v => v.Items)
            .Where(v => v.CompanyId == companyId
                && string.Compare(v.VoucherDate, fromDate) >= 0
                && string.Compare(v.VoucherDate, toDate) <= 0)
            .ToListAsync(ct);
    }

    public async Task<Voucher?> GetWithItemsAsync(int voucherId, CancellationToken ct = default)
    {
        return await DbSet.Include(v => v.Items)
            .FirstOrDefaultAsync(v => v.Id == voucherId, ct);
    }

    // X5 — شماره‌گذاریِ متوالیِ بدونِ‌خلأ و اتمیک (مقاوم به هم‌روندی).
    // قبلاً stub بود و `V{ticks}` می‌داد (نه متوالی، نامناسبِ حسابداریِ رسمیِ ایرانی).
    // جدولِ توالیِ سبک `dbo.__DocSequences` نگه‌داری می‌شود؛ ردیفِ هر شرکت یک‌بار از بیشترین
    // شمارهٔ عددیِ موجود seed و سپس به‌صورتِ اتمیک (UPDATE با اسنادِ @next=Last=Last+1) افزایش می‌یابد.
    // اگر در تراکنشِ جاری فراخوانده شود، در همان تراکنش شرکت می‌کند (رول‌بک → شماره مصرف نمی‌شود).
    public async Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default)
    {
        var conn = Context.Database.GetDbConnection();
        var wasClosed = conn.State != System.Data.ConnectionState.Open;
        if (wasClosed) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = conn.CreateCommand();
            var tx = Context.Database.CurrentTransaction?.GetDbTransaction();
            if (tx != null) cmd.Transaction = tx;
            cmd.CommandText = @"
IF OBJECT_ID('dbo.__DocSequences','U') IS NULL
  CREATE TABLE dbo.__DocSequences(CompanyId INT NOT NULL, Name NVARCHAR(40) NOT NULL, LastValue INT NOT NULL,
     CONSTRAINT PK___DocSequences PRIMARY KEY (CompanyId, Name));
IF NOT EXISTS (SELECT 1 FROM dbo.__DocSequences WHERE CompanyId=@c AND Name=@n)
  INSERT dbo.__DocSequences(CompanyId, Name, LastValue)
  VALUES (@c, @n, ISNULL((SELECT MAX(TRY_CONVERT(INT, VoucherNumber)) FROM Acc.Vouchers WHERE CompanyId=@c), 0));
DECLARE @next INT;
UPDATE dbo.__DocSequences SET @next = LastValue = LastValue + 1 WHERE CompanyId=@c AND Name=@n;
SELECT @next;";
            var pc = cmd.CreateParameter(); pc.ParameterName = "@c"; pc.Value = companyId; cmd.Parameters.Add(pc);
            var pn = cmd.CreateParameter(); pn.ParameterName = "@n"; pn.Value = "voucher"; cmd.Parameters.Add(pn);
            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result).ToString();
        }
        finally { if (wasClosed) await conn.CloseAsync(); }
    }
}

public class AccountRepository : GenericRepository<Account>, IAccountRepository
{
    public AccountRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default)
        => await DbSet.Where(a => a.CompanyId == companyId).ToListAsync(ct);

    public async Task<Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);

    public async Task<List<Account>> GetChildrenAsync(int parentId, CancellationToken ct = default)
        => await DbSet.Where(a => a.ParentId == parentId).ToListAsync(ct);

    public async Task<List<Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default)
        => await DbSet.Where(a => a.CompanyId == companyId && a.IsLeaf).ToListAsync(ct);

    public async Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default)
        => await DbSet.Where(a => a.Id == accountId)
            .SelectMany(a => a.VoucherItems)
            .AnyAsync(ct);

    public async Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default)
    {
        var account = await DbSet.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account == null) return 0;
        // Simplified calculation
        return 0; // In real impl: query voucher items
    }
}

public class ChequeRepository : GenericRepository<Cheque>, IChequeRepository
{
    public ChequeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<Cheque>> GetByStatusAsync(
        int companyId, ChequeStatus status, CancellationToken ct = default)
        => await DbSet.Where(c => c.CompanyId == companyId && c.Status == status).ToListAsync(ct);

    public async Task<List<Cheque>> GetDueTodayAsync(int companyId, CancellationToken ct = default)
    {
        var today = DateTime.Now.ToString("yyyy/MM/dd");
        return await DbSet.Where(c => c.CompanyId == companyId
            && c.Status == ChequeStatus.InProcess
            && string.Compare(c.DueDate, today) == 0).ToListAsync(ct);
    }

    public async Task<List<Cheque>> GetOverdueAsync(int companyId, CancellationToken ct = default)
    {
        var today = DateTime.Now.ToString("yyyy/MM/dd");
        return await DbSet.Where(c => c.CompanyId == companyId
            && c.Status == ChequeStatus.InProcess
            && string.Compare(c.DueDate, today) < 0).ToListAsync(ct);
    }
}

// Inventory Repositories
public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Product?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Code == code, ct);

    public async Task<Product?> GetByBarcodeAsync(int companyId, string barcode, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(p => p.CompanyId == companyId && p.Barcode == barcode, ct);

    public async Task<List<Product>> SearchAsync(int companyId, string searchText, CancellationToken ct = default)
        => await DbSet.Where(p => p.CompanyId == companyId &&
            (p.Code.Contains(searchText) || p.Name.Contains(searchText)))
            .ToListAsync(ct);

    public async Task<List<Product>> GetByGroupAsync(int groupId, CancellationToken ct = default)
        => await DbSet.Where(p => p.GroupId == groupId).ToListAsync(ct);

    public async Task<List<Product>> GetLowStockAsync(int companyId, CancellationToken ct = default)
        => await DbSet.Where(p => p.CompanyId == companyId && p.IsActive).ToListAsync(ct);
}

public class WarehouseRepository : GenericRepository<Warehouse>, IWarehouseRepository
{
    public WarehouseRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<Warehouse>> GetByCompanyAsync(int companyId, CancellationToken ct = default)
        => await DbSet.Where(w => w.CompanyId == companyId).ToListAsync(ct);

    public async Task<Warehouse?> GetDefaultAsync(int companyId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(w => w.CompanyId == companyId && w.IsDefault, ct);
}

public class StockItemRepository : GenericRepository<StockItem>, IStockItemRepository
{
    public StockItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StockItem?> GetByProductAndWarehouseAsync(
        int productId, int warehouseId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(
            s => s.ProductId == productId && s.WarehouseId == warehouseId, ct);

    public async Task<List<StockItem>> GetByProductAsync(int productId, CancellationToken ct = default)
        => await DbSet.Where(s => s.ProductId == productId).ToListAsync(ct);

    public async Task<List<StockItem>> GetByWarehouseAsync(int warehouseId, CancellationToken ct = default)
        => await DbSet.Where(s => s.WarehouseId == warehouseId).ToListAsync(ct);

    public async Task<decimal> GetTotalQuantityAsync(int productId, CancellationToken ct = default)
        => await DbSet.Where(s => s.ProductId == productId).SumAsync(s => s.Quantity, ct);
}

// Restaurant Repositories (v2)
public class RestaurantOrderRepository
    : GenericRepository<SamaHesab.Domain.Entities.Restaurant.RestaurantOrder>, IRestaurantOrderRepository
{
    public RestaurantOrderRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SamaHesab.Domain.Entities.Restaurant.RestaurantOrder?> GetWithItemsAsync(
        int id, CancellationToken ct = default)
        => await DbSet.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<int> CountByCompanyAsync(int companyId, CancellationToken ct = default)
        => await DbSet.CountAsync(o => o.CompanyId == companyId, ct);
}

// Voucher Templates (productivity)
public class VoucherTemplateRepository
    : GenericRepository<SamaHesab.Domain.Entities.Accounting.VoucherTemplate>, IVoucherTemplateRepository
{
    public VoucherTemplateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SamaHesab.Domain.Entities.Accounting.VoucherTemplate?> GetWithLinesAsync(
        int id, CancellationToken ct = default)
        => await DbSet.Include(t => t.Lines).FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<List<SamaHesab.Domain.Entities.Accounting.VoucherTemplate>> GetByCompanyAsync(
        int companyId, CancellationToken ct = default)
        => await DbSet.Include(t => t.Lines).Where(t => t.CompanyId == companyId && t.IsActive).ToListAsync(ct);
}

// Recurring Vouchers (productivity)
public class RecurringVoucherRepository
    : GenericRepository<SamaHesab.Domain.Entities.Accounting.RecurringVoucher>, IRecurringVoucherRepository
{
    public RecurringVoucherRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<SamaHesab.Domain.Entities.Accounting.RecurringVoucher>> GetActiveAsync(
        int companyId, CancellationToken ct = default)
        => await DbSet.Where(r => r.CompanyId == companyId && r.IsActive).ToListAsync(ct);
}

// User Favorites / Recent / Pinned (productivity)
public class UserItemRefRepository
    : GenericRepository<SamaHesab.Domain.Entities.Settings.UserItemRef>, IUserItemRefRepository
{
    public UserItemRefRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SamaHesab.Domain.Entities.Settings.UserItemRef?> FindAsync(
        int companyId, int userId, string entityType, int entityId, CancellationToken ct = default)
        => await DbSet.FirstOrDefaultAsync(r => r.CompanyId == companyId && r.UserId == userId
            && r.EntityType == entityType && r.EntityId == entityId, ct);

    public async Task<List<SamaHesab.Domain.Entities.Settings.UserItemRef>> RecentAsync(
        int companyId, int userId, string entityType, int top, CancellationToken ct = default)
        => await DbSet.Where(r => r.CompanyId == companyId && r.UserId == userId && r.EntityType == entityType)
            .OrderByDescending(r => r.LastUsedAt).Take(top).ToListAsync(ct);

    public async Task<List<SamaHesab.Domain.Entities.Settings.UserItemRef>> PinnedAsync(
        int companyId, int userId, string entityType, CancellationToken ct = default)
        => await DbSet.Where(r => r.CompanyId == companyId && r.UserId == userId
            && r.EntityType == entityType && r.Pinned)
            .OrderBy(r => r.Label).ToListAsync(ct);
}

// Stock Count (انبارگردانی) — کار #۲۸
public class StockCountRepository
    : GenericRepository<SamaHesab.Domain.Entities.Inventory.StockCountSession>, IStockCountRepository
{
    public StockCountRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SamaHesab.Domain.Entities.Inventory.StockCountSession?> GetWithLinesAsync(
        int id, CancellationToken ct = default)
        => await DbSet.Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id, ct);
}
