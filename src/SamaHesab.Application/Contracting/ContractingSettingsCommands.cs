using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Contracting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Contracting;

/// <summary>CON-C1-2 — تنظیماتِ پیمانکاری (نگاشتِ حساب + درصدهای پیش‌فرض). هیچ AccountId/نرخ هاردکد نمی‌شود.</summary>
public record ContractingSettingsDto(
    int? ReceivableAccountId, int? RetentionDepositAccountId, int? InsuranceDepositAccountId, int? PrepaidTaxAccountId,
    int? AdvanceLiabilityAccountId, int? PenaltyExpenseAccountId, int? RevenueAccountId, int? BankAccountId,
    decimal DefaultAdvancePercent, decimal DefaultRetentionPercent,
    decimal DefaultInsuranceWithholdPercent, decimal DefaultTaxWithholdPercent, bool UseCostCenterAsDimension)
{
    public static ContractingSettingsDto Default() =>
        new(null, null, null, null, null, null, null, null, 0, 0, 0, 0, false);

    public static ContractingSettingsDto From(ContractingSetting s) => new(
        s.ReceivableAccountId, s.RetentionDepositAccountId, s.InsuranceDepositAccountId, s.PrepaidTaxAccountId,
        s.AdvanceLiabilityAccountId, s.PenaltyExpenseAccountId, s.RevenueAccountId, s.BankAccountId,
        s.DefaultAdvancePercent, s.DefaultRetentionPercent, s.DefaultInsuranceWithholdPercent,
        s.DefaultTaxWithholdPercent, s.UseCostCenterAsDimension);
}

// ── خواندن ──
public record GetContractingSettingsQuery() : IRequest<ContractingSettingsDto>;

public class GetContractingSettingsQueryHandler : IRequestHandler<GetContractingSettingsQuery, ContractingSettingsDto>
{
    private readonly IRepository<ContractingSetting> _settings;
    private readonly ICurrentUserService _user;
    public GetContractingSettingsQueryHandler(IRepository<ContractingSetting> settings, ICurrentUserService user)
    { _settings = settings; _user = user; }

    public async Task<ContractingSettingsDto> Handle(GetContractingSettingsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var row = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        return row is null ? ContractingSettingsDto.Default() : ContractingSettingsDto.From(row);
    }
}

// ── ذخیره (upsert) ──
public record SaveContractingSettingsCommand(ContractingSettingsDto Settings) : IRequest<Result<int>>;

public class SaveContractingSettingsCommandHandler : IRequestHandler<SaveContractingSettingsCommand, Result<int>>
{
    private readonly IRepository<ContractingSetting> _settings;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveContractingSettingsCommandHandler(IRepository<ContractingSetting> settings, IUnitOfWork uow, ICurrentUserService user)
    { _settings = settings; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveContractingSettingsCommand req, CancellationToken ct)
    {
        var d = req.Settings;
        var companyId = _user.CompanyId ?? 1;
        var row = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        var isNew = row is null;
        row ??= ContractingSetting.Create(companyId);
        row.Update(d.ReceivableAccountId, d.RetentionDepositAccountId, d.InsuranceDepositAccountId, d.PrepaidTaxAccountId,
            d.AdvanceLiabilityAccountId, d.PenaltyExpenseAccountId, d.RevenueAccountId, d.BankAccountId,
            d.DefaultAdvancePercent, d.DefaultRetentionPercent, d.DefaultInsuranceWithholdPercent,
            d.DefaultTaxWithholdPercent, d.UseCostCenterAsDimension);
        if (isNew) await _settings.AddAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(row.Id);
    }
}
