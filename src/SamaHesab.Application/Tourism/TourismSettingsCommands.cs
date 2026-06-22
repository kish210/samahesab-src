using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Tourism;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Tourism;

/// <summary>TUR-C1-2 — تنظیماتِ گردشگری (نگاشتِ حساب‌های کنترلی + پرچم‌ها). هیچ AccountId هاردکد نمی‌شود.</summary>
public record TourismSettingsDto(
    int? CashAccountId, int? ReceivableAccountId, int? RevenueAccountId, int? CogsAccountId,
    int? SupplierDepositAccountId, int? SalesDiscountAccountId, int? DepositDifferenceAccountId,
    int? CommissionExpenseAccountId, int? SalespersonPayableAccountId, int? BankAccountId,
    bool SaleBaseAfterDiscountDefault, decimal LowDepositThreshold, bool PostPerSale, bool CommissionThroughPayroll)
{
    public static TourismSettingsDto Default() =>
        new(null, null, null, null, null, null, null, null, null, null, true, 0, true, true);

    public static TourismSettingsDto From(TourismSetting s) => new(
        s.CashAccountId, s.ReceivableAccountId, s.RevenueAccountId, s.CogsAccountId,
        s.SupplierDepositAccountId, s.SalesDiscountAccountId, s.DepositDifferenceAccountId,
        s.CommissionExpenseAccountId, s.SalespersonPayableAccountId, s.BankAccountId,
        s.SaleBaseAfterDiscountDefault, s.LowDepositThreshold, s.PostPerSale, s.CommissionThroughPayroll);
}

// ── خواندن ──
public record GetTourismSettingsQuery() : IRequest<TourismSettingsDto>;

public class GetTourismSettingsQueryHandler : IRequestHandler<GetTourismSettingsQuery, TourismSettingsDto>
{
    private readonly IRepository<TourismSetting> _settings;
    private readonly ICurrentUserService _user;
    public GetTourismSettingsQueryHandler(IRepository<TourismSetting> settings, ICurrentUserService user)
    { _settings = settings; _user = user; }

    public async Task<TourismSettingsDto> Handle(GetTourismSettingsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var row = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        return row is null ? TourismSettingsDto.Default() : TourismSettingsDto.From(row);
    }
}

// ── ذخیره (upsert) ──
public record SaveTourismSettingsCommand(TourismSettingsDto Settings) : IRequest<Result<int>>;

public class SaveTourismSettingsCommandHandler : IRequestHandler<SaveTourismSettingsCommand, Result<int>>
{
    private readonly IRepository<TourismSetting> _settings;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveTourismSettingsCommandHandler(IRepository<TourismSetting> settings, IUnitOfWork uow, ICurrentUserService user)
    { _settings = settings; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveTourismSettingsCommand req, CancellationToken ct)
    {
        var d = req.Settings;
        var companyId = _user.CompanyId ?? 1;
        var row = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        var isNew = row is null;
        row ??= TourismSetting.Create(companyId);
        row.Update(d.CashAccountId, d.ReceivableAccountId, d.RevenueAccountId, d.CogsAccountId,
            d.SupplierDepositAccountId, d.SalesDiscountAccountId, d.DepositDifferenceAccountId,
            d.CommissionExpenseAccountId, d.SalespersonPayableAccountId, d.BankAccountId,
            d.SaleBaseAfterDiscountDefault, d.LowDepositThreshold, d.PostPerSale, d.CommissionThroughPayroll);
        if (isNew) await _settings.AddAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(row.Id);
    }
}
