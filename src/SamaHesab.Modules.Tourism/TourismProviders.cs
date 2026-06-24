using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Tourism.Application;
using SamaHesab.Modules.Tourism.Application.Commands;
using SamaHesab.Modules.Tourism.Domain;

namespace SamaHesab.Modules.Tourism;

/// <summary>پیاده‌سازیِ قلابِ پورسانتِ هسته (decouple HR↔Tourism) — از پلِ پورسانتِ ماژول استفاده می‌کند.</summary>
public sealed class TourismSalesCommissionProvider : ISalesCommissionProvider
{
    private readonly IRepository<SalesCommissionEntry> _commissions;
    private readonly IRepository<Party> _parties;
    public TourismSalesCommissionProvider(IRepository<SalesCommissionEntry> commissions, IRepository<Party> parties)
    { _commissions = commissions; _parties = parties; }

    public Task<Dictionary<int, decimal>> CommissionByEmployeeAsync(
        IReadOnlyList<Employee> employees, int companyId, string persianYearMonth, CancellationToken ct)
        => CommissionPayrollBridge.ByEmployeeAsync(_commissions, _parties, employees, companyId, persianYearMonth, ct);
}

/// <summary>پیاده‌سازیِ قلابِ هشدارِ ودیعهٔ کمِ هسته — تعدادِ تأمین‌کنندگانِ کم‌ودیعه.</summary>
public sealed class TourismSupplierDepositAlertProvider : ISupplierDepositAlertProvider
{
    private readonly IMediator _mediator;
    public TourismSupplierDepositAlertProvider(IMediator mediator) => _mediator = mediator;

    public async Task<int> LowDepositCountAsync(CancellationToken ct)
        => (await _mediator.Send(new GetSupplierDepositBalancesQuery(OnlyLow: true), ct)).Count;
}
