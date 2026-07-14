using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Accounting;

public class Cheque : AuditableEntity
{
    public int BranchId { get; private set; }
    public ChequeType ChequeType { get; private set; }
    public string ChequeNumber { get; private set; } = default!;
    public string BankName { get; private set; } = default!;
    public string? AccountNumber { get; private set; }
    public string? BranchName { get; private set; }
    public decimal Amount { get; private set; }
    public string DueDate { get; private set; } = default!;
    public string? IssueDate { get; private set; }
    public string? IssuedBy { get; private set; }
    public string? Description { get; private set; }
    public ChequeStatus Status { get; private set; } = ChequeStatus.InProcess;
    public int? AccountId { get; private set; }
    public int? PartyId { get; private set; }
    public string? PartyType { get; private set; }
    public int? ReceiveVoucherId { get; private set; }
    public int? PayVoucherId { get; private set; }
    public int? ClearedVoucherId { get; private set; }
    public DateTime? ReturnedAt { get; private set; }
    public string? ReturnReason { get; private set; }

    private Cheque() { }

    public static Cheque Create(int companyId, int branchId, ChequeType chequeType,
        string chequeNumber, string bankName, decimal amount, string dueDate,
        string? issuedBy = null, string? description = null)
    {
        if (amount <= 0) throw new ArgumentException("مبلغ چک باید بزرگتر از صفر باشد.");
        if (string.IsNullOrWhiteSpace(chequeNumber)) throw new ArgumentException("شماره چک الزامی است.");

        return new Cheque
        {
            CompanyId = companyId,
            BranchId = branchId,
            ChequeType = chequeType,
            ChequeNumber = chequeNumber,
            BankName = bankName,
            Amount = amount,
            DueDate = dueDate,
            IssuedBy = issuedBy,
            Description = description
        };
    }

    public void SetParty(int partyId, string partyType)
    {
        PartyId = partyId;
        PartyType = partyType;
    }

    /// <summary>پیوندِ سندِ ثبتِ چکِ دریافتی (انتقالِ حسابِ دریافتنی → اسنادِ دریافتنی).</summary>
    public void SetReceiveVoucher(int voucherId)
    {
        ReceiveVoucherId = voucherId;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>U-ACCT-1.2 — پیوندِ سندِ صدورِ چکِ پرداختنی (بازطبقه‌بندیِ بدهی از پرداختنیِ
    /// عمومی به اسنادِ پرداختنی-چک، ۳-۰۲-۰۰۱). PayVoucherId از قبل در موجودیت تعریف شده بود
    /// ولی هیچ‌جا ست نمی‌شد.</summary>
    public void SetPayVoucher(int voucherId)
    {
        PayVoucherId = voucherId;
        UpdatedAt = DateTime.Now;
    }

    public void Clear(int voucherId)
    {
        if (Status != ChequeStatus.InProcess)
            throw new InvalidOperationException("فقط چک‌های در جریان قابل وصول هستند.");
        Status = ChequeStatus.Cleared;
        ClearedVoucherId = voucherId;
        UpdatedAt = DateTime.Now;
    }

    public void Return(string reason)
    {
        if (Status != ChequeStatus.InProcess)
            throw new InvalidOperationException("فقط چک‌های در جریان قابل برگشت هستند.");
        Status = ChequeStatus.Returned;
        ReturnReason = reason;
        ReturnedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }

    public void Transfer()
    {
        if (Status != ChequeStatus.InProcess)
            throw new InvalidOperationException("فقط چک‌های در جریان قابل واگذاری هستند.");
        Status = ChequeStatus.Transferred;
        UpdatedAt = DateTime.Now;
    }

    public void Cancel()
    {
        if (Status == ChequeStatus.Cleared)
            throw new InvalidOperationException("چک وصول شده قابل ابطال نیست.");
        Status = ChequeStatus.Cancelled;
        UpdatedAt = DateTime.Now;
    }

    public bool IsOverdue(string todayPersianDate) =>
        Status == ChequeStatus.InProcess && string.Compare(DueDate, todayPersianDate) < 0;
}
