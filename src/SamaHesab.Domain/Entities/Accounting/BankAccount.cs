using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Accounting;

public class BankAccount : BaseEntity
{
    public int CompanyId { get; private set; }
    public int? BranchId { get; private set; }
    public int AccountId { get; private set; }
    public string BankName { get; private set; } = default!;
    public string AccountNumber { get; private set; } = default!;
    public string? ShebaNumber { get; private set; }
    public string? CardNumber { get; private set; }
    public string? BranchName { get; private set; }
    public string? BranchCode { get; private set; }
    public decimal OpeningBalance { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    private BankAccount() { }

    public static BankAccount Create(int companyId, int accountId, string bankName,
        string accountNumber, string? shebaNumber = null, string? cardNumber = null,
        string? branchName = null, decimal openingBalance = 0)
    {
        return new BankAccount
        {
            CompanyId = companyId,
            AccountId = accountId,
            BankName = bankName,
            AccountNumber = accountNumber,
            ShebaNumber = shebaNumber,
            CardNumber = cardNumber,
            BranchName = branchName,
            OpeningBalance = openingBalance
        };
    }
}
