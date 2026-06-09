using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Treasury.Queries;

/// <summary>
/// فهرست پرداختنی‌ها: تأمین‌کنندگانِ بستانکار، مرتب بر اساس مبلغ (بزرگ‌ترین اول) —
/// نقطه‌ی شروعِ گردش‌کار مدیریت سررسید و پرداخت‌ها.
/// </summary>
public record GetPayablesQuery(int MaxResults = 200) : IRequest<List<PayableDto>>;

public record PayableDto(
    int SupplierId,
    string Name,
    string? Mobile,
    decimal Balance);

public class GetPayablesQueryHandler : IRequestHandler<GetPayablesQuery, List<PayableDto>>
{
    private readonly IRepository<Supplier> _suppliers;
    private readonly ICurrentUserService _currentUser;

    public GetPayablesQueryHandler(IRepository<Supplier> suppliers, ICurrentUserService currentUser)
    {
        _suppliers = suppliers;
        _currentUser = currentUser;
    }

    public async Task<List<PayableDto>> Handle(GetPayablesQuery request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var creditors = await _suppliers.FindAsync(
            s => s.CompanyId == companyId && s.IsActive && s.Balance > 0.01m, ct);

        return creditors
            .OrderByDescending(s => s.Balance)
            .Take(request.MaxResults)
            .Select(s => new PayableDto(s.Id, s.FullName, s.Mobile, s.Balance))
            .ToList();
    }
}
