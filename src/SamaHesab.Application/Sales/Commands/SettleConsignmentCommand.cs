using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Commands;

/// <summary>
/// U-CONSIGN-SETTLE — وقتی کنسینی واقعاً کالای امانی را به مشتریِ نهایی فروخت: کالا از حسابِ
/// «کالای امانی نزدِ دیگران» (۱-۰۵-۰۰۳) خارج و سندِ فروشِ واقعی (درآمد/COGS/دریافتنی) زده می‌شود.
/// موجودیِ انبار دوباره کم نمی‌شود — کالا از زمانِ ارسالِ کنسینمنت از انبار خارج شده بود.
/// بهایِ تمام‌شده از رویِ همان مبلغی که سندِ اصلیِ کنسینمنت به ۱-۰۵-۰۰۳ بدهکار کرده بود خوانده
/// می‌شود (نه بازمحاسبهٔ FIFO) — دقیقاً همان مبلغ است، پس سند همیشه متوازن می‌ماند.
/// </summary>
public record SettleConsignmentCommand(
    int ConsignmentInvoiceId,
    string SettlementDate,
    decimal PaidAmount = 0,
    string PaymentMethod = "نسیه",
    int? BankAccountId = null
) : IRequest<Result<int>>;

public class SettleConsignmentCommandValidator : AbstractValidator<SettleConsignmentCommand>
{
    public SettleConsignmentCommandValidator()
    {
        RuleFor(x => x.ConsignmentInvoiceId).GreaterThan(0).WithMessage("فاکتورِ کنسینمنت الزامی است.");
        RuleFor(x => x.SettlementDate).NotEmpty().WithMessage("تاریخِ تسویه الزامی است.");
        RuleFor(x => x.PaidAmount).GreaterThanOrEqualTo(0).WithMessage("مبلغِ دریافتی نمی‌تواند منفی باشد.");
    }
}

public class SettleConsignmentCommandHandler : IRequestHandler<SettleConsignmentCommand, Result<int>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly IRepository<Domain.Entities.CRM.Party> _customers;
    private readonly IRepository<Domain.Entities.Accounting.BankAccount> _bankAccounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public SettleConsignmentCommandHandler(IRepository<SalesInvoice> invoices, IVoucherRepository vouchers,
        IAccountRepository accounts, IRepository<Domain.Entities.CRM.Party> customers,
        IRepository<Domain.Entities.Accounting.BankAccount> bankAccounts,
        IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _invoices = invoices; _vouchers = vouchers; _accounts = accounts;
        _customers = customers; _bankAccounts = bankAccounts;
        _unitOfWork = unitOfWork; _currentUser = currentUser;
    }

    public async Task<Result<int>> Handle(SettleConsignmentCommand req, CancellationToken ct)
    {
        var invoice = await _invoices.GetByIdAsync(req.ConsignmentInvoiceId, ct);
        if (invoice == null) return Result<int>.Failure("فاکتورِ کنسینمنت یافت نشد.");
        if (invoice.InvoiceType != InvoiceType.Consignment)
            return Result<int>.Failure("این فاکتور از نوعِ کنسینمنت نیست.");
        if (invoice.SettledVoucherId.HasValue)
            return Result<int>.Failure("این کنسینمنت قبلاً تسویه شده است.");
        if (!invoice.VoucherId.HasValue)
            return Result<int>.Failure("سندِ حسابداریِ اصلیِ کنسینمنت یافت نشد.");

        var companyId = invoice.CompanyId;
        var originalVoucher = await _vouchers.GetWithItemsAsync(invoice.VoucherId.Value, ct);
        var consignmentOut = await _accounts.GetByCodeAsync(companyId, "1-05-003", ct);
        if (originalVoucher == null || consignmentOut == null)
            return Result<int>.Failure("سندِ اصلی یا حسابِ کالای امانی یافت نشد.");

        var totalCost = originalVoucher.Items.FirstOrDefault(i => i.AccountId == consignmentOut.Id)?.Debit ?? 0;
        if (totalCost <= 0) return Result<int>.Failure("مبلغِ بهایِ تمام‌شدهٔ کنسینمنتِ اصلی یافت نشد.");

        var receivable = await _accounts.GetByCodeAsync(companyId, "1-03-001", ct);
        var sales = await _accounts.GetByCodeAsync(companyId, "6-01-001", ct);
        var vat = await _accounts.GetByCodeAsync(companyId, "3-04-001", ct);
        var cogs = await _accounts.GetByCodeAsync(companyId, Inventory.Commands.InventoryAccounting.Cogs, ct);
        if (receivable == null || sales == null || cogs == null)
            return Result<int>.Failure("نمودارِ حساب‌ها برایِ تسویهٔ کنسینمنت کامل نیست.");

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var grand = invoice.GrandTotal;
            var salesAmount = grand - invoice.TotalTax;
            if (salesAmount < 0) salesAmount = 0;
            var paid = req.PaidAmount > 0 ? Math.Min(req.PaidAmount, grand) : 0;
            var remain = grand - paid;

            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var voucher = Voucher.Create(companyId, invoice.BranchId, invoice.FiscalYearId,
                number, req.SettlementDate, 3 /*Sale*/,
                $"سندِ تسویهٔ کنسینمنت {invoice.InvoiceNumber}", invoice.InvoiceNumber);

            int row = 1;
            if (paid > 0)
            {
                Account? payAcc = req.PaymentMethod switch
                {
                    "نقدی" => await _accounts.GetByCodeAsync(companyId, "1-01-001", ct),
                    "چک" => await _accounts.GetByCodeAsync(companyId, "1-04-001", ct),
                    "بانک" => await Inventory.Commands.InventoryAccounting.ResolveBankAccountAsync(
                        _accounts, _bankAccounts, companyId, req.BankAccountId, ct),
                    _ => await _accounts.GetByCodeAsync(companyId, "1-01-001", ct)
                };
                payAcc ??= await _accounts.GetByCodeAsync(companyId, "1-01-001", ct);
                if (payAcc != null)
                    voucher.AddItem(VoucherItem.Create(0, row++, payAcc.Id, paid, 0, $"دریافتِ وجهِ تسویهٔ کنسینمنت ({req.PaymentMethod})"));
                else remain = grand;
            }
            if (remain > 0)
                voucher.AddItem(VoucherItem.Create(0, row++, receivable.Id, remain, 0, $"تسویهٔ کنسینمنت {invoice.InvoiceNumber}"));

            voucher.AddItem(VoucherItem.Create(0, row++, sales.Id, 0, salesAmount, "درآمدِ فروشِ کنسینمنتِ تسویه‌شده"));
            if (invoice.TotalTax > 0 && vat != null)
                voucher.AddItem(VoucherItem.Create(0, row++, vat.Id, 0, invoice.TotalTax, "مالیات بر ارزش افزوده"));

            // بهایِ تمام‌شده: خروجِ کالا از «کالای امانی نزدِ دیگران» (نه موجودیِ انبار — قبلاً خارج شده).
            voucher.AddItem(VoucherItem.Create(0, row++, cogs.Id, totalCost, 0, "بهایِ تمام‌شدهٔ کنسینمنتِ تسویه‌شده"));
            voucher.AddItem(VoucherItem.Create(0, row++, consignmentOut.Id, 0, totalCost, "خروجِ کالایِ امانیِ تسویه‌شده"));

            await _vouchers.AddAsync(voucher, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            if (voucher.CanPost())
                voucher.Post(_currentUser.UserId ?? 1);
            _vouchers.Update(voucher);

            invoice.SetSettled(voucher.Id);
            _invoices.Update(invoice);

            // مانده به مشتری (کنسینی) اضافه می‌شود — هم‌راستا با U-PARTY-BAL برای فروشِ عادی.
            if (remain > 0)
            {
                var customer = await _customers.GetByIdAsync(invoice.CustomerId, ct);
                if (customer != null)
                    customer.UpdateBalance(customer.Balance + remain);
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);
            return Result<int>.Success(voucher.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}

// ── فهرستِ کنسینمنت‌هایِ بازِ تسویه‌نشده ──────────────────────────────────────
public record OpenConsignmentRow(int InvoiceId, string Number, string Date, string CustomerName,
    decimal GrandTotal, decimal RemainAmount);

public record GetOpenConsignmentsQuery : IRequest<List<OpenConsignmentRow>>;

public class GetOpenConsignmentsQueryHandler : IRequestHandler<GetOpenConsignmentsQuery, List<OpenConsignmentRow>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Domain.Entities.CRM.Party> _customers;
    private readonly ICurrentUserService _currentUser;

    public GetOpenConsignmentsQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<Domain.Entities.CRM.Party> customers, ICurrentUserService currentUser)
    { _invoices = invoices; _customers = customers; _currentUser = currentUser; }

    public async Task<List<OpenConsignmentRow>> Handle(GetOpenConsignmentsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _invoices.FindAsync(i => i.CompanyId == companyId
            && i.InvoiceType == InvoiceType.Consignment && i.SettledVoucherId == null, ct);
        var customers = (await _customers.FindAsync(c => c.CompanyId == companyId, ct))
            .ToDictionary(c => c.Id, c => c.FullName);

        return list.OrderByDescending(i => i.Id)
            .Select(i => new OpenConsignmentRow(i.Id, i.InvoiceNumber, i.InvoiceDate,
                customers.TryGetValue(i.CustomerId, out var name) ? name : $"#{i.CustomerId}",
                i.GrandTotal, i.RemainAmount))
            .ToList();
    }
}
