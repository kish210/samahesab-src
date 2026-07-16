using FluentValidation;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Treasury.Commands;

/// <summary>پرداخت وجه به تأمین‌کننده (treasury payment) — posts a voucher and reduces the supplier balance.</summary>
public record CreatePaymentCommand(
    int BranchId, int FiscalYearId, string Date, int SupplierId, decimal Amount,
    string PaymentMethod = "نقدی", string? Description = null,
    // U-ACCT-1.3: اگر تعیین شود، اول همین فاکتور (تا سقفِ ماندهٔ خودش) تخصیص می‌گیرد؛
    // باقی طبقِ FIFOِ فعلی روی بقیهٔ فاکتورهایِ بازِ تأمین‌کننده.
    int? InvoiceId = null,
    // U-ACCT-1.4: اگر تعیین شود و روش پرداخت «بانک» باشد، به‌جایِ بانکِ پیش‌فرضِ تک‌بانکی از
    // حسابِ GLِ همین BankAccount استفاده می‌شود.
    int? BankAccountId = null) : IRequest<Result<int>>;

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage("تأمین‌کننده الزامی است.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("مبلغ باید بزرگتر از صفر باشد.");
    }
}

public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<Party> _suppliers;
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IRepository<BankAccount> _bankAccounts;
    private readonly IRepository<Domain.Entities.CRM.PartyLedgerEntry> _partyLedger;

    public CreatePaymentCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser,
        IAccountRepository accounts, IVoucherRepository vouchers, IRepository<Party> suppliers,
        IRepository<PurchaseInvoice> invoices, IRepository<FiscalYear> fiscalYears,
        IRepository<BankAccount> bankAccounts, IRepository<Domain.Entities.CRM.PartyLedgerEntry> partyLedger)
    { _uow = uow; _currentUser = currentUser; _accounts = accounts; _vouchers = vouchers; _suppliers = suppliers; _invoices = invoices; _fiscalYears = fiscalYears; _bankAccounts = bankAccounts; _partyLedger = partyLedger; }

    public async Task<Result<int>> Handle(CreatePaymentCommand req, CancellationToken ct)
    {
        // قفل دوره: پرداخت در سال مالیِ بسته یا با تاریخِ خارج از بازه مجاز نیست.
        var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
        var lockMsg = FiscalPeriodGuard.Check(fy, req.Date);
        if (lockMsg is not null) return Result<int>.Failure(lockMsg);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;
            // U-ACCT-1.4: «بانک» حالا از BankAccountِ انتخاب‌شده (اگر داده شود) resolve می‌شود.
            Account? creditAcc = req.PaymentMethod switch
            {
                "بانک" => await Inventory.Commands.InventoryAccounting.ResolveBankAccountAsync(
                    _accounts, _bankAccounts, companyId, req.BankAccountId, ct),
                "چک"   => await _accounts.GetByCodeAsync(companyId, "1-04-001", ct),
                _       => await _accounts.GetByCodeAsync(companyId, "1-01-001", ct)
            };
            creditAcc ??= await _accounts.GetByCodeAsync(companyId, "1-01-001", ct);
            var payable = await _accounts.GetByCodeAsync(companyId, "3-01-001", ct);
            if (creditAcc == null || payable == null)
                return Result<int>.Failure("حساب‌های خزانه/پرداختنی تعریف نشده‌اند.");

            // ── تخصیص (قبل از ساختِ سند، تا سهمِ پیش‌پرداخت از مبلغِ کل معلوم شود) ──
            // U-ACCT-1.3: اگر فاکتورِ مشخصی هدف گرفته شده، اول همان (تا سقفِ ماندهٔ خودش).
            var open = await _invoices.FindAsync(
                i => i.SupplierId == req.SupplierId && i.StatusCode == "قطعی"
                     && i.RemainAmount > 0.01m, ct);
            var remaining = req.Amount;
            if (req.InvoiceId is int targetId)
            {
                var target = open.FirstOrDefault(i => i.Id == targetId);
                if (target != null)
                {
                    var apply = Math.Min(remaining, target.RemainAmount);
                    if (apply > 0) { target.SetPaid(target.PaidAmount + apply); remaining -= apply; }
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
                inv.SetPaid(inv.PaidAmount + line.Applied);
            }

            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                10 /*پرداخت*/, req.Description ?? $"پرداخت وجه به تأمین‌کننده");
            int row = 1;
            var appliedToInvoices = req.Amount - unapplied;
            if (appliedToInvoices > 0)
                v.AddItem(VoucherItem.Create(0, row++, payable.Id, appliedToInvoices, 0, "بابت بدهی به تأمین‌کننده"));
            // U-ACCT-1.3: مازادِ بیشتر از مجموعِ ماندهٔ فاکتورهایِ باز، پیش‌تر بی‌سروصدا دور ریخته
            // می‌شد. این مازاد یک پیش‌پرداخت (دارایی) است، نه صرفاً کاهشِ بدهیِ پرداختنی.
            if (unapplied > 0.01m)
            {
                var advance = await _accounts.GetByCodeAsync(companyId, "1-06-002", ct);
                v.AddItem(VoucherItem.Create(0, row++, (advance ?? payable).Id, unapplied, 0,
                    advance != null ? "پیش‌پرداخت به تأمین‌کننده" : "بابت بدهی به تأمین‌کننده (بدونِ حسابِ پیش‌پرداخت)"));
            }
            v.AddItem(VoucherItem.Create(0, row++, creditAcc.Id, 0, req.Amount, $"پرداخت ({req.PaymentMethod})"));
            await _vouchers.AddAsync(v, ct);

            var supplier = await _suppliers.GetByIdAsync(req.SupplierId, ct);
            if (supplier != null)
                await CRM.PartyLedger.RecordAsync(_partyLedger, supplier, -req.Amount, req.Date,
                    "پرداخت", null, req.Description ?? "پرداخت وجه به تأمین‌کننده", ct);

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
