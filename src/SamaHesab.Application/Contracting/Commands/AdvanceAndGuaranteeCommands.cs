using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Contracting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Contracting.Commands;

// ════════════════════════════════════════════════════════════════════════════
// CON-C1-4 — پیش‌پرداختِ دریافتی (سندِ Dr بانک / Cr بدهیِ پیش‌پرداخت).
// ════════════════════════════════════════════════════════════════════════════
public record ReceiveAdvanceCommand(
    int BranchId, int FiscalYearId, string Date, int ContractProjectId, decimal Amount,
    string PaymentMethod = "بانک", string? Note = null) : IRequest<Result<int>>;

public class ReceiveAdvanceCommandHandler : IRequestHandler<ReceiveAdvanceCommand, Result<int>>
{
    private readonly IRepository<ContractProject> _projects;
    private readonly IRepository<ContractingSetting> _settings;
    private readonly IRepository<AdvancePayment> _advances;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public ReceiveAdvanceCommandHandler(IRepository<ContractProject> projects, IRepository<ContractingSetting> settings,
        IRepository<AdvancePayment> advances, IVoucherRepository vouchers, IRepository<FiscalYear> fiscalYears,
        IUnitOfWork uow, ICurrentUserService user)
    { _projects = projects; _settings = settings; _advances = advances; _vouchers = vouchers; _fiscalYears = fiscalYears; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(ReceiveAdvanceCommand req, CancellationToken ct)
    {
        if (req.Amount <= 0) return Result<int>.Failure("مبلغِ پیش‌پرداخت باید بزرگ‌تر از صفر باشد.");
        var companyId = _user.CompanyId ?? 1;
        var project = await _projects.FindSingleAsync(p => p.Id == req.ContractProjectId && p.CompanyId == companyId, ct);
        if (project is null) return Result<int>.Failure("پیمان یافت نشد.");

        var set = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        if (set?.AdvanceLiabilityAccountId is null) return Result<int>.Failure("حسابِ بدهیِ پیش‌پرداخت در تنظیمات تعریف نشده.");
        var bankAcc = set.BankAccountId;
        if (bankAcc is null) return Result<int>.Failure("حسابِ بانک در تنظیماتِ پیمانکاری تعریف نشده.");

        var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
        var lockMsg = FiscalPeriodGuard.Check(fy, req.Date);
        if (lockMsg is not null) return Result<int>.Failure(lockMsg);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                9, $"پیش‌پرداختِ دریافتی — پیمانِ {project.Code}", $"CON-ADV-{number}");
            v.AddItem(VoucherItem.Create(0, 1, bankAcc.Value, req.Amount, 0, $"دریافتِ پیش‌پرداخت ({req.PaymentMethod})"));
            v.AddItem(VoucherItem.Create(0, 2, set.AdvanceLiabilityAccountId.Value, 0, req.Amount, "بدهیِ پیش‌پرداختِ کارفرما"));
            v.Post(_user.UserId ?? 0);
            await _vouchers.AddAsync(v, ct);
            await _uow.SaveChangesAsync(ct);

            var adv = AdvancePayment.Create(companyId, project.Id, req.Amount, req.Date, req.PaymentMethod, req.Note);
            adv.SetVoucher(v.Id);
            await _advances.AddAsync(adv, ct);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(adv.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}

// ════════════════════════════════════════════════════════════════════════════
// CON-C1-5 — ضمانت‌نامه (ثبت/آزادسازی) + آزادسازیِ سپردهٔ حسن‌انجام/بیمه (سندِ Dr بانک / Cr سپرده).
// ════════════════════════════════════════════════════════════════════════════
public record RegisterGuaranteeCommand(
    int ContractProjectId, GuaranteeType Type, string Bank, decimal Amount,
    string IssueDate, string ExpiryDate, string? Note = null) : IRequest<Result<int>>;

public class RegisterGuaranteeCommandHandler : IRequestHandler<RegisterGuaranteeCommand, Result<int>>
{
    private readonly IRepository<Guarantee> _guarantees;
    private readonly IRepository<ContractProject> _projects;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public RegisterGuaranteeCommandHandler(IRepository<Guarantee> guarantees, IRepository<ContractProject> projects, IUnitOfWork uow, ICurrentUserService user)
    { _guarantees = guarantees; _projects = projects; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(RegisterGuaranteeCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        if (!await _projects.AnyAsync(p => p.Id == req.ContractProjectId && p.CompanyId == companyId, ct))
            return Result<int>.Failure("پیمان یافت نشد.");
        Guarantee g;
        try { g = Guarantee.Create(companyId, req.ContractProjectId, req.Type, req.Bank, req.Amount, req.IssueDate, req.ExpiryDate, req.Note); }
        catch (ArgumentException ex) { return Result<int>.Failure(ex.Message); }
        await _guarantees.AddAsync(g, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(g.Id);
    }
}

public record ReleaseGuaranteeCommand(int GuaranteeId) : IRequest<Result<int>>;

public class ReleaseGuaranteeCommandHandler : IRequestHandler<ReleaseGuaranteeCommand, Result<int>>
{
    private readonly IRepository<Guarantee> _guarantees;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public ReleaseGuaranteeCommandHandler(IRepository<Guarantee> guarantees, IUnitOfWork uow, ICurrentUserService user)
    { _guarantees = guarantees; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(ReleaseGuaranteeCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var g = await _guarantees.FindSingleAsync(x => x.Id == req.GuaranteeId && x.CompanyId == companyId, ct);
        if (g is null) return Result<int>.Failure("ضمانت‌نامه یافت نشد.");
        g.Release();
        _guarantees.Update(g);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(g.Id);
    }
}

/// <summary>آزادسازیِ سپردهٔ حسن‌انجام‌کار یا بیمه پس از مفاصاحساب: Dr بانک / Cr داراییِ سپرده.</summary>
public record ReleaseDepositCommand(
    int BranchId, int FiscalYearId, string Date, DeductionType DepositType, decimal Amount, string? Note = null)
    : IRequest<Result<int>>;

public class ReleaseDepositCommandHandler : IRequestHandler<ReleaseDepositCommand, Result<int>>
{
    private readonly IRepository<ContractingSetting> _settings;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public ReleaseDepositCommandHandler(IRepository<ContractingSetting> settings, IVoucherRepository vouchers,
        IRepository<FiscalYear> fiscalYears, IUnitOfWork uow, ICurrentUserService user)
    { _settings = settings; _vouchers = vouchers; _fiscalYears = fiscalYears; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(ReleaseDepositCommand req, CancellationToken ct)
    {
        if (req.Amount <= 0) return Result<int>.Failure("مبلغِ آزادسازی باید بزرگ‌تر از صفر باشد.");
        if (req.DepositType is not (DeductionType.Retention or DeductionType.Insurance))
            return Result<int>.Failure("نوعِ سپرده باید حسن‌انجام‌کار یا بیمه باشد.");
        var companyId = _user.CompanyId ?? 1;
        var set = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        var depositAcc = req.DepositType == DeductionType.Retention ? set?.RetentionDepositAccountId : set?.InsuranceDepositAccountId;
        if (set?.BankAccountId is null || depositAcc is null)
            return Result<int>.Failure("حسابِ بانک/سپرده در تنظیماتِ پیمانکاری تعریف نشده.");

        var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
        var lockMsg = FiscalPeriodGuard.Check(fy, req.Date);
        if (lockMsg is not null) return Result<int>.Failure(lockMsg);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var label = req.DepositType == DeductionType.Retention ? "حسن‌انجام‌کار" : "بیمه";
            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                9, $"آزادسازیِ سپردهٔ {label}", $"CON-REL-{number}");
            v.AddItem(VoucherItem.Create(0, 1, set.BankAccountId.Value, req.Amount, 0, $"وصولِ سپردهٔ {label}"));
            v.AddItem(VoucherItem.Create(0, 2, depositAcc.Value, 0, req.Amount, $"آزادسازیِ سپردهٔ {label}"));
            v.Post(_user.UserId ?? 0);
            await _vouchers.AddAsync(v, ct);
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
