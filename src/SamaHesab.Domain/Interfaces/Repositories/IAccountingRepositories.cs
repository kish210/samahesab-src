using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Interfaces.Repositories;

/// <summary>U-DB-PAGING — جمعِ بدهکار/بستانکارِ یک حساب در یک بازه (DB-level GroupBy+Sum؛ برایِ تراز آزمایشی).</summary>
public record AccountMovementTotal(int AccountId, decimal Debit, decimal Credit);

public interface IVoucherRepository : IRepository<Entities.Accounting.Voucher>
{
    Task<List<Entities.Accounting.Voucher>> GetByDateRangeAsync(
        int companyId, int fiscalYearId, string fromDate, string toDate, CancellationToken ct = default);

    /// <summary>Vouchers (with their items eager-loaded) in a date range — used by financial reports.</summary>
    Task<List<Entities.Accounting.Voucher>> GetByDateRangeWithItemsAsync(
        int companyId, string fromDate, string toDate, CancellationToken ct = default);

    Task<Entities.Accounting.Voucher?> GetWithItemsAsync(int voucherId, CancellationToken ct = default);
    Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default);

    // ── U-DB-PAGING (@2026-07-16) — نسخه‌هایِ DB-levelِ کوئری‌هایِ گزارش، به‌جایِ بارگذاریِ کلِ
    // بازهٔ تاریخ در حافظه. پیش‌فرضِ default-interface-method (throw) تا فیک‌ریپازیتوری‌هایِ
    // تستِ موجود (که این متدها را صدا نمی‌زنند) نیازی به تغییر نداشته باشند.

    /// <summary>صفحه‌بندیِ واقعیِ DB-level (OFFSET/FETCH) برایِ لیستِ اسناد + شمارشِ کل.</summary>
    Task<(List<Entities.Accounting.Voucher> Items, int TotalCount)> GetPagedByDateRangeAsync(
        int companyId, int fiscalYearId, string fromDate, string toDate,
        int? status, string? searchText, int pageNumber, int pageSize, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>مجموعِ بدهکار−بستانکارِ یک حساب پیش از تاریخِ مشخص (ماندهٔ ابتدا) — یک SUMِ DB-level، بدونِ بارگذاریِ تاریخچه.</summary>
    Task<decimal> SumAccountMovementBeforeAsync(
        int companyId, int accountId, string beforeDate, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>ردیف‌هایِ یک حساب در بازهٔ تاریخ، مرتب و DB-level (برایِ دفترِ معین).</summary>
    Task<List<Entities.Accounting.VoucherItem>> GetAccountItemsInRangeAsync(
        int companyId, int accountId, string fromDate, string toDate, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>ردیف‌هایِ دفترِ کل در بازهٔ تاریخ با فیلترهایِ اختیاری، مرتب و DB-level.</summary>
    Task<List<Entities.Accounting.VoucherItem>> GetLedgerItemsInRangeAsync(
        int companyId, string fromDate, string toDate,
        int? accountId, int? costCenterId, int? projectId, int? branchId, CancellationToken ct = default)
        => throw new NotImplementedException();

    /// <summary>جمعِ بدهکار/بستانکار به‌ازایِ هر حساب در بازهٔ تاریخ (DB-level GroupBy+Sum؛ برایِ تراز آزمایشی).</summary>
    Task<List<AccountMovementTotal>> GetAccountTotalsInRangeAsync(
        int companyId, string fromDate, string toDate,
        int? costCenterId, int? projectId, int? branchId, CancellationToken ct = default)
        => throw new NotImplementedException();
}

public interface IAccountRepository : IRepository<Entities.Accounting.Account>
{
    Task<List<Entities.Accounting.Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default);
    Task<Entities.Accounting.Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default);
    Task<List<Entities.Accounting.Account>> GetChildrenAsync(int parentId, CancellationToken ct = default);
    Task<List<Entities.Accounting.Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default);
    Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default);
}

public interface IChequeRepository : IRepository<Entities.Accounting.Cheque>
{
    Task<List<Entities.Accounting.Cheque>> GetByStatusAsync(
        int companyId, ChequeStatus status, CancellationToken ct = default);
    Task<List<Entities.Accounting.Cheque>> GetDueTodayAsync(int companyId, CancellationToken ct = default);
    Task<List<Entities.Accounting.Cheque>> GetOverdueAsync(int companyId, CancellationToken ct = default);
}
