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
    string PaymentMethod = "نقدی", string? Description = null) : IRequest<Result<int>>;

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
    private readonly IRepository<Supplier> _suppliers;
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IRepository<FiscalYear> _fiscalYears;

    public CreatePaymentCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser,
        IAccountRepository accounts, IVoucherRepository vouchers, IRepository<Supplier> suppliers,
        IRepository<PurchaseInvoice> invoices, IRepository<FiscalYear> fiscalYears)
    { _uow = uow; _currentUser = currentUser; _accounts = accounts; _vouchers = vouchers; _suppliers = suppliers; _invoices = invoices; _fiscalYears = fiscalYears; }

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
            var payCode = req.PaymentMethod switch
            {
                "بانک" => "1-02-001",
                "چک"   => "1-04-001",
                _       => "1-01-001"
            };
            var creditAcc = await _accounts.GetByCodeAsync(companyId, payCode, ct)
                            ?? await _accounts.GetByCodeAsync(companyId, "1-01-001", ct);
            var payable = await _accounts.GetByCodeAsync(companyId, "3-01-001", ct);
            if (creditAcc == null || payable == null)
                return Result<int>.Failure("حساب‌های خزانه/پرداختنی تعریف نشده‌اند.");

            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                10 /*پرداخت*/, req.Description ?? $"پرداخت وجه به تأمین‌کننده");
            v.AddItem(VoucherItem.Create(0, 1, payable.Id, req.Amount, 0, "بابت بدهی به تأمین‌کننده"));
            v.AddItem(VoucherItem.Create(0, 2, creditAcc.Id, 0, req.Amount, $"پرداخت ({req.PaymentMethod})"));
            await _vouchers.AddAsync(v, ct);

            var supplier = await _suppliers.GetByIdAsync(req.SupplierId, ct);
            if (supplier != null) supplier.UpdateBalance(supplier.Balance - req.Amount);

            // تخصیص خودکار FIFO به فاکتورهای خریدِ بازِ تأمین‌کننده (قدیمی‌ترین اول)
            var open = await _invoices.FindAsync(
                i => i.SupplierId == req.SupplierId && i.StatusCode == "قطعی"
                     && i.RemainAmount > 0.01m, ct);
            var ordered = open
                .OrderBy(i => i.InvoiceDate)          // تاریخ شمسی yyyy/MM/dd → مرتب‌سازی لغوی = زمانی
                .ThenBy(i => i.InvoiceNumber)
                .Select(i => (i.Id, i.RemainAmount));
            var (lines, _) = PaymentAllocation.AllocateFifo(req.Amount, ordered);
            foreach (var line in lines)
            {
                var inv = open.First(i => i.Id == line.InvoiceId);
                inv.SetPaid(inv.PaidAmount + line.Applied);
            }

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
