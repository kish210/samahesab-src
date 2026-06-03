using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Interfaces.Repositories;

public interface IVoucherRepository : IRepository<Entities.Accounting.Voucher>
{
    Task<List<Entities.Accounting.Voucher>> GetByDateRangeAsync(
        int companyId, int fiscalYearId, string fromDate, string toDate, CancellationToken ct = default);

    /// <summary>Vouchers (with their items eager-loaded) in a date range — used by financial reports.</summary>
    Task<List<Entities.Accounting.Voucher>> GetByDateRangeWithItemsAsync(
        int companyId, string fromDate, string toDate, CancellationToken ct = default);

    Task<Entities.Accounting.Voucher?> GetWithItemsAsync(int voucherId, CancellationToken ct = default);
    Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default);
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
