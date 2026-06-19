using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>وضعیت اعتبار یک مشتری: مانده، سقف، اعتبار باقی‌مانده و آیا از سقف عبور کرده.</summary>
public record GetCustomerCreditQuery(int CustomerId) : IRequest<CustomerCreditDto?>;

public record CustomerCreditDto(int CustomerId, string Name, decimal Balance,
    decimal CreditLimit, decimal Available, bool IsOverLimit);

public class GetCustomerCreditQueryHandler : IRequestHandler<GetCustomerCreditQuery, CustomerCreditDto?>
{
    private readonly IRepository<Party> _customers;
    public GetCustomerCreditQueryHandler(IRepository<Party> customers) => _customers = customers;

    public async Task<CustomerCreditDto?> Handle(GetCustomerCreditQuery req, CancellationToken ct)
    {
        var c = await _customers.GetByIdAsync(req.CustomerId, ct);
        if (c is null) return null;
        var name = string.IsNullOrWhiteSpace(c.CompanyName) ? $"{c.FirstName} {c.LastName}".Trim() : c.CompanyName!;
        var available = CreditLimitPolicy.Available(c.Balance, c.CreditLimit);
        return new CustomerCreditDto(c.Id, name, c.Balance, c.CreditLimit,
            available == decimal.MaxValue ? -1 : available,            // -1 = نامحدود
            IsOverLimit: c.CreditLimit > 0 && c.Balance > c.CreditLimit);
    }
}
