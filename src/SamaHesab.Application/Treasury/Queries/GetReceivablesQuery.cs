using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Treasury.Queries;

/// <summary>
/// فهرست دریافتنی‌ها: مشتریان بدهکار، مرتب بر اساس مبلغ (بزرگ‌ترین اول) —
/// نقطه‌ی شروعِ گردش‌کار وصول مطالبات. از همین فهرست «ثبت دریافت» سریع انجام می‌شود.
/// </summary>
public record GetReceivablesQuery(int MaxResults = 200) : IRequest<List<ReceivableDto>>;

public record ReceivableDto(
    int CustomerId,
    string Name,
    string? Mobile,
    decimal Balance,
    decimal CreditLimit,
    bool IsOverCreditLimit);

public class GetReceivablesQueryHandler : IRequestHandler<GetReceivablesQuery, List<ReceivableDto>>
{
    private readonly IRepository<Customer> _customers;
    private readonly ICurrentUserService _currentUser;

    public GetReceivablesQueryHandler(IRepository<Customer> customers, ICurrentUserService currentUser)
    {
        _customers = customers;
        _currentUser = currentUser;
    }

    public async Task<List<ReceivableDto>> Handle(GetReceivablesQuery request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var debtors = await _customers.FindAsync(
            c => c.CompanyId == companyId && c.IsActive && c.Balance > 0.01m, ct);

        return debtors
            .OrderByDescending(c => c.Balance)
            .Take(request.MaxResults)
            .Select(c => new ReceivableDto(
                c.Id, c.FullName, c.Mobile, c.Balance, c.CreditLimit,
                IsOverCreditLimit: c.CreditLimit > 0 && c.Balance > c.CreditLimit))
            .ToList();
    }
}
