using FluentValidation;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Treasury.Commands;

/// <summary>دریافت وجه از مشتری (treasury receipt) — posts a voucher and reduces the customer balance.</summary>
public record CreateReceiptCommand(
    int BranchId, int FiscalYearId, string Date, int CustomerId, decimal Amount,
    string PaymentMethod = "نقدی", string? Description = null,
    // U-ACCT-1.3: اگر تعیین شود، اول همین فاکتور (تا سقفِ ماندهٔ خودش) تخصیص می‌گیرد؛
    // باقی طبقِ FIFOِ فعلی روی بقیهٔ فاکتورهایِ بازِ مشتری.
    int? InvoiceId = null,
    // U-ACCT-1.4: اگر تعیین شود و روش پرداخت «بانک» باشد، به‌جایِ بانکِ پیش‌فرضِ تک‌بانکی از
    // حسابِ GLِ همین BankAccount استفاده می‌شود.
    int? BankAccountId = null) : IRequest<Result<int>>;

public class CreateReceiptCommandValidator : AbstractValidator<CreateReceiptCommand>
{
    public CreateReceiptCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری الزامی است.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("مبلغ باید بزرگتر از صفر باشد.");
    }
}

public class CreateReceiptCommandHandler : IRequestHandler<CreateReceiptCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<Party> _customers;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IRepository<BankAccount> _bankAccounts;
    private readonly IRepository<Domain.Entities.CRM.PartyLedgerEntry> _partyLedger;

    public CreateReceiptCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser,
        IAccountRepository accounts, IVoucherRepository vouchers, IRepository<Party> customers,
        IRepository<SalesInvoice> invoices, IRepository<FiscalYear> fiscalYears,
        IRepository<BankAccount> bankAccounts, IRepository<Domain.Entities.CRM.PartyLedgerEntry> partyLedger)
    { _uow = uow; _currentUser = currentUser; _accounts = accounts; _vouchers = vouchers; _customers = customers; _invoices = invoices; _fiscalYears = fiscalYears; _bankAccounts = bankAccounts; _partyLedger = partyLedger; }

    public async Task<Result<int>> Handle(CreateReceiptCommand req, CancellationToken ct)
    {
        // قفل دوره: دریافت در سال مالیِ بسته یا با تاریخِ خارج از بازه مجاز نیست.
        var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
        var lockMsg = FiscalPeriodGuard.Check(fy, req.Date);
        if (lockMsg is not null) return Result<int>.Failure(lockMsg);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;
            // U-ACCT-1.4: «بانک» حالا از BankAccountِ انتخاب‌شده (اگر داده شود) resolve می‌شود.
            Account? debitAcc = req.PaymentMethod switch
            {
                "بانک" => await Inventory.Commands.InventoryAccounting.ResolveBankAccountAsync(
                    _accounts, _bankAccounts, companyId, req.BankAccountId, ct),
                "چک"   => await _accounts.GetByCodeAsync(companyId, "1-04-001", ct),
                _       => await _accounts.GetByCodeAsync(companyId, "1-01-001", ct) // نقدی → صندوق
            };
            debitAcc ??= await _accounts.GetByCodeAsync(companyId, "1-01-001", ct);
            var receivable = await _accounts.GetByCodeAsync(companyId, "1-03-001", ct);
            if (debitAcc == null || receivable == null)
                return Result<int>.Failure("حساب‌های خزانه/دریافتنی تعریف نشده‌اند.");

            // ── تخصیص (قبل از ساختِ سند، تا سهمِ پیش‌دریافت از مبلغِ کل معلوم شود) ──
            // U-ACCT-1.3: اگر فاکتورِ مشخصی هدف گرفته شده، اول همان (تا سقفِ ماندهٔ خودش).
            var open = await _invoices.FindAsync(
                i => i.CustomerId == req.CustomerId && i.Status == InvoiceStatus.Posted
                     && i.RemainAmount > 0.01m, ct);
            var remaining = req.Amount;
            if (req.InvoiceId is int targetId)
            {
                var target = open.FirstOrDefault(i => i.Id == targetId);
                if (target != null)
                {
                    var apply = Math.Min(remaining, target.RemainAmount);
                    if (apply > 0) { target.AddPayment(apply); remaining -= apply; }
                }
            }
            var ordered = open
                .Where(i => req.InvoiceId is null || i.Id != req.InvoiceId)
                .OrderBy(i => i.InvoiceDate)          // تاریخ شمسی yyyy/MM/dd → مرتب‌سازی لغوی = زمانی
                .ThenBy(i => i.InvoiceNumber)
                .Select(i => (i.Id, i.RemainAmount));
            var (lines, unapplied) = PaymentAllocation.AllocateFifo(remaining, ordered);
            foreach (var line in lines)
            {
                var inv = open.First(i => i.Id == line.InvoiceId);
                inv.AddPayment(line.Applied);
            }

            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                11 /*دریافت*/, req.Description ?? $"دریافت وجه از مشتری");
            int row = 1;
            v.AddItem(VoucherItem.Create(0, row++, debitAcc.Id, req.Amount, 0, $"دریافت ({req.PaymentMethod})"));
            var appliedToInvoices = req.Amount - unapplied;
            if (appliedToInvoices > 0)
                v.AddItem(VoucherItem.Create(0, row++, receivable.Id, 0, appliedToInvoices, "بابت بدهی مشتری"));
            // U-ACCT-1.3: مازادِ بیشتر از مجموعِ ماندهٔ فاکتورهایِ باز، پیش‌تر بی‌سروصدا دور ریخته
            // می‌شد (Cr کاملاً به ۱-۰۳-۰۰۱ می‌رفت، بدونِ ردی از اینکه چه مقدارش واقعاً به فاکتوری
            // نخورده). این مازاد یک بدهیِ «پیش‌دریافت» است، نه کاهشِ دارایی — طبقه‌بندیِ درستِ
            // صورت‌هایِ مالی نیاز به حسابِ جدا دارد (۳-۰۳-۰۰۱، اگر تعریف شده باشد؛ وگرنه fallback
            // به رفتارِ قدیمی برایِ سازگاری).
            if (unapplied > 0.01m)
            {
                var advance = await _accounts.GetByCodeAsync(companyId, "3-03-001", ct);
                v.AddItem(VoucherItem.Create(0, row++, (advance ?? receivable).Id, 0, unapplied,
                    advance != null ? "پیش‌دریافت از مشتری" : "بابت بدهی مشتری (بدونِ حسابِ پیش‌دریافت)"));
            }
            await _vouchers.AddAsync(v, ct);

            var customer = await _customers.GetByIdAsync(req.CustomerId, ct);
            if (customer != null)
                await CRM.PartyLedger.RecordAsync(_partyLedger, customer, -req.Amount, req.Date,
                    "دریافت", null, req.Description ?? "دریافت وجه از مشتری", ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(v.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}
