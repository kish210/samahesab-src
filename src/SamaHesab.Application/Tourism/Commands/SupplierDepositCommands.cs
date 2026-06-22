using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Tourism;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Tourism.Commands;

/// <summary>
/// TUR-C1-4 — شارژِ ودیعه/اعتبارِ نزدِ تأمین‌کننده. سندِ خودش را می‌زند: Dr ودیعه(کنترلی) / Cr بانک‌یا‌نقد.
/// (مغایرت‌گیریِ بانکی سالم می‌ماند چون حسابِ بانک عادی بستانکار می‌شود.)
/// </summary>
public record TopUpSupplierDepositCommand(
    int BranchId, int FiscalYearId, string Date, int SupplierPartyId, decimal Amount,
    string PaymentMethod = "بانک", string? Note = null) : IRequest<Result<int>>;

public class TopUpSupplierDepositCommandHandler : IRequestHandler<TopUpSupplierDepositCommand, Result<int>>
{
    private readonly IRepository<TourismSetting> _settings;
    private readonly IRepository<SupplierDeposit> _deposits;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public TopUpSupplierDepositCommandHandler(IRepository<TourismSetting> settings, IRepository<SupplierDeposit> deposits,
        IVoucherRepository vouchers, IRepository<FiscalYear> fiscalYears,
        IUnitOfWork uow, ICurrentUserService user)
    {
        _settings = settings; _deposits = deposits; _vouchers = vouchers;
        _fiscalYears = fiscalYears; _uow = uow; _user = user;
    }

    public async Task<Result<int>> Handle(TopUpSupplierDepositCommand req, CancellationToken ct)
    {
        if (req.SupplierPartyId <= 0) return Result<int>.Failure("تأمین‌کننده الزامی است.");
        if (req.Amount <= 0) return Result<int>.Failure("مبلغِ شارژ باید بزرگ‌تر از صفر باشد.");
        var companyId = _user.CompanyId ?? 1;

        var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
        var lockMsg = FiscalPeriodGuard.Check(fy, req.Date);
        if (lockMsg is not null) return Result<int>.Failure(lockMsg);

        var set = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        if (set?.SupplierDepositAccountId is null)
            return Result<int>.Failure("حسابِ ودیعه در تنظیماتِ گردشگری تعریف نشده.");

        // حسابِ پرداخت (طرفِ بستانکار) از تنظیمات یا روشِ پرداخت.
        var creditAccId = req.PaymentMethod switch
        {
            "بانک" => set.BankAccountId ?? set.CashAccountId,
            _      => set.CashAccountId ?? set.BankAccountId
        };
        if (creditAccId is null) return Result<int>.Failure("حسابِ بانک/صندوق در تنظیماتِ گردشگری تعریف نشده.");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                9 /*عمومی*/, $"شارژِ ودیعهٔ تأمین‌کننده {req.SupplierPartyId}", $"TUR-DEP-{number}");
            v.AddItem(VoucherItem.Create(0, 1, set.SupplierDepositAccountId.Value, req.Amount, 0,
                $"شارژِ ودیعهٔ تأمین‌کننده {req.SupplierPartyId}"));
            v.AddItem(VoucherItem.Create(0, 2, creditAccId.Value, 0, req.Amount,
                $"پرداختِ شارژِ ودیعه ({req.PaymentMethod})"));
            v.Post(_user.UserId ?? 0);
            await _vouchers.AddAsync(v, ct);
            await _uow.SaveChangesAsync(ct);

            var dep = SupplierDeposit.Create(companyId, req.SupplierPartyId, req.Amount, req.Date, req.PaymentMethod, req.Note);
            dep.SetVoucher(v.Id);
            await _deposits.AddAsync(dep, ct);
            await _uow.SaveChangesAsync(ct);

            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(dep.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}

/// <summary>TUR-C1-4 — ماندهٔ ودیعهٔ هر تأمین‌کننده = جمعِ شارژها − برداشتِ فروش. آلارمِ ودیعهٔ کم.</summary>
public record GetSupplierDepositBalancesQuery(bool OnlyLow = false) : IRequest<List<SupplierDepositBalanceDto>>;

public record SupplierDepositBalanceDto(
    int SupplierPartyId, string SupplierName,
    decimal ToppedUp, decimal Drawn, decimal Remaining, bool IsLow);

public class GetSupplierDepositBalancesQueryHandler
    : IRequestHandler<GetSupplierDepositBalancesQuery, List<SupplierDepositBalanceDto>>
{
    private readonly IRepository<TourismSetting> _settings;
    private readonly IRepository<SupplierDeposit> _deposits;
    private readonly IRepository<TourismSaleLine> _lines;
    private readonly IRepository<Party> _parties;
    private readonly ICurrentUserService _user;

    public GetSupplierDepositBalancesQueryHandler(IRepository<TourismSetting> settings,
        IRepository<SupplierDeposit> deposits, IRepository<TourismSaleLine> lines,
        IRepository<Party> parties, ICurrentUserService user)
    { _settings = settings; _deposits = deposits; _lines = lines; _parties = parties; _user = user; }

    public async Task<List<SupplierDepositBalanceDto>> Handle(GetSupplierDepositBalancesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var threshold = (await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct))?.LowDepositThreshold ?? 0;

        var topUps = (await _deposits.FindAsync(d => d.CompanyId == companyId, ct))
            .GroupBy(d => d.SupplierPartyId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Amount));

        // برداشت = جمعِ بهای خطوطِ فروش per تأمین‌کننده (snapshotِ UnitCost).
        var drawn = (await _lines.FindAsync(_ => true, ct))
            .GroupBy(l => l.SupplierPartyId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity * l.UnitCost));

        var supplierIds = topUps.Keys.Union(drawn.Keys).ToHashSet();
        if (supplierIds.Count == 0) return new();

        var names = (await _parties.FindAsync(p => p.CompanyId == companyId, ct))
            .Where(p => supplierIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => p.FullName);

        var list = supplierIds.Select(id =>
        {
            var up = topUps.GetValueOrDefault(id);
            var dn = drawn.GetValueOrDefault(id);
            var rem = up - dn;
            return new SupplierDepositBalanceDto(id, names.GetValueOrDefault(id, $"#{id}"), up, dn, rem,
                IsLow: rem < threshold);
        });

        if (req.OnlyLow) list = list.Where(x => x.IsLow);
        return list.OrderBy(x => x.Remaining).ToList();
    }
}
