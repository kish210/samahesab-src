using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Events;

namespace SamaHesab.Domain.Entities.Accounting;

public class Voucher : AuditableEntity, IBranchScoped
{
    public int BranchId { get; private set; }
    public int FiscalYearId { get; private set; }
    public string VoucherNumber { get; private set; } = default!;
    public string VoucherDate { get; private set; } = default!;
    public int VoucherTypeId { get; private set; }
    public VoucherStatus Status { get; private set; } = VoucherStatus.Draft;
    public string? Description { get; private set; }
    public string? Reference { get; private set; }
    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }
    public int? CurrencyId { get; private set; }
    public decimal ExchangeRate { get; private set; } = 1;
    public int? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public int? ReversedFromId { get; private set; }
    public bool IsReversed { get; private set; }

    /// <summary>گردش‌کارِ تأیید (T22). null = خارج از گردش‌کار (قابلِ قطعیِ مستقیم، سازگارِ عقب‌رو).</summary>
    public VoucherApprovalStatus? ApprovalStatus { get; private set; }

    public ICollection<VoucherItem> Items { get; private set; } = new List<VoucherItem>();

    private Voucher() { }

    public static Voucher Create(int companyId, int branchId, int fiscalYearId, string voucherNumber,
        string voucherDate, int voucherTypeId, string? description = null, string? reference = null)
    {
        return new Voucher
        {
            CompanyId = companyId,
            BranchId = branchId,
            FiscalYearId = fiscalYearId,
            VoucherNumber = voucherNumber,
            VoucherDate = voucherDate,
            VoucherTypeId = voucherTypeId,
            Description = description,
            Reference = reference
        };
    }

    public void AddItem(VoucherItem item)
    {
        if (Status != VoucherStatus.Draft)
            throw new InvalidOperationException("فقط اسناد پیش‌نویس قابل ویرایش هستند.");
        Items.Add(item);
        RecalculateTotals();
    }

    public void RemoveItem(int itemId)
    {
        if (Status != VoucherStatus.Draft)
            throw new InvalidOperationException("فقط اسناد پیش‌نویس قابل ویرایش هستند.");
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("ردیف سند یافت نشد.");
        Items.Remove(item);
        RecalculateTotals();
    }

    public void Post(int userId)
    {
        if (Status != VoucherStatus.Draft)
            throw new InvalidOperationException("فقط اسناد پیش‌نویس را می‌توان قطعی کرد.");

        // گردش‌کارِ تأیید (T22، opt-in): سندی که وارد گردش‌کار شده، تا تأیید قطعی نمی‌شود.
        // ApprovalStatus == null یعنی خارج از گردش‌کار → مثل قبل قطعی می‌شود (سازگارِ عقب‌رو).
        if (ApprovalStatus is VoucherApprovalStatus.PendingApproval)
            throw new InvalidOperationException("سند در انتظارِ تأیید است؛ پیش از تأیید قابلِ قطعی‌کردن نیست.");
        if (ApprovalStatus is VoucherApprovalStatus.Rejected)
            throw new InvalidOperationException("سند ردشده است؛ ابتدا آن را به پیش‌نویس بازگردانید.");

        if (!IsBalanced())
            throw new InvalidOperationException("سند تراز نیست. مجموع بدهکار و بستانکار باید برابر باشد.");

        if (!Items.Any())
            throw new InvalidOperationException("سند باید حداقل یک ردیف داشته باشد.");

        Status = VoucherStatus.Posted;
        ApprovedByUserId = userId;
        ApprovedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;

        AddDomainEvent(new VoucherPostedEvent(Id, CompanyId, userId));
    }

    public void MakePermanent()
    {
        if (Status != VoucherStatus.Posted)
            throw new InvalidOperationException("فقط اسناد قطعی را می‌توان دائمی کرد.");
        Status = VoucherStatus.Permanent;
        UpdatedAt = DateTime.Now;
    }

    public void MarkAsReversed() { IsReversed = true; UpdatedAt = DateTime.Now; }

    /// <summary>این سند را به‌عنوان «سند برگشتی» سندِ دیگری علامت می‌زند.</summary>
    public void SetAsReversalOf(int originalVoucherId)
    {
        ReversedFromId = originalVoucherId;
        UpdatedAt = DateTime.Now;
    }

    private void RecalculateTotals()
    {
        TotalDebit = Items.Sum(i => i.Debit);
        TotalCredit = Items.Sum(i => i.Credit);
    }

    // ── گردش‌کارِ تأیید (T22) — گذارهای معتبر مطابقِ ApprovalWorkflow ──
    /// <summary>ارسالِ سندِ پیش‌نویس برای تأیید (Draft → PendingApproval).</summary>
    public void SubmitForApproval()
    {
        if (Status != VoucherStatus.Draft)
            throw new InvalidOperationException("فقط سندِ پیش‌نویس را می‌توان برای تأیید ارسال کرد.");
        if (ApprovalStatus is VoucherApprovalStatus.PendingApproval or VoucherApprovalStatus.Approved)
            throw new InvalidOperationException("سند از قبل در گردش‌کارِ تأیید است.");
        ApprovalStatus = VoucherApprovalStatus.PendingApproval;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>تأییدِ سندِ در انتظار (PendingApproval → Approved).</summary>
    public void ApproveBy(int userId)
    {
        if (ApprovalStatus != VoucherApprovalStatus.PendingApproval)
            throw new InvalidOperationException("فقط سندِ «در انتظارِ تأیید» را می‌توان تأیید کرد.");
        ApprovalStatus = VoucherApprovalStatus.Approved;
        ApprovedByUserId = userId;
        ApprovedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>ردِّ سندِ در انتظار (PendingApproval → Rejected).</summary>
    public void RejectApproval()
    {
        if (ApprovalStatus != VoucherApprovalStatus.PendingApproval)
            throw new InvalidOperationException("فقط سندِ «در انتظارِ تأیید» را می‌توان رد کرد.");
        ApprovalStatus = VoucherApprovalStatus.Rejected;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>بازگرداندنِ سندِ ردشده به پیش‌نویس/خارج از گردش‌کار (Rejected → null).</summary>
    public void ReopenApproval()
    {
        if (ApprovalStatus != VoucherApprovalStatus.Rejected)
            throw new InvalidOperationException("فقط سندِ ردشده را می‌توان بازگرداند.");
        ApprovalStatus = null;
        UpdatedAt = DateTime.Now;
    }

    public bool IsBalanced() => Math.Abs(TotalDebit - TotalCredit) < 0.01m;
    public bool CanEdit() => Status == VoucherStatus.Draft;
    public bool CanPost() => Status == VoucherStatus.Draft && IsBalanced() && Items.Any();
    public bool CanReverse() => Status >= VoucherStatus.Posted && !IsReversed;
}
