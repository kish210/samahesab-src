using MediatR;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.CRM.Domain;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>موجودیِ امتیازِ مشتری + آخرین تراکنش‌های باشگاه.</summary>
public record GetCustomerLoyaltyQuery(int CustomerId, int Recent = 10) : IRequest<CustomerLoyaltyDto>;

public record LoyaltyTxnDto(int Points, string Type, string? Description, DateTime Date);
public record CustomerLoyaltyDto(int CustomerId, int Balance, List<LoyaltyTxnDto> Recent);

public class GetCustomerLoyaltyQueryHandler : IRequestHandler<GetCustomerLoyaltyQuery, CustomerLoyaltyDto>
{
    private readonly IRepository<LoyaltyTransaction> _loyalty;
    public GetCustomerLoyaltyQueryHandler(IRepository<LoyaltyTransaction> loyalty) => _loyalty = loyalty;

    public async Task<CustomerLoyaltyDto> Handle(GetCustomerLoyaltyQuery req, CancellationToken ct)
    {
        var txns = await _loyalty.FindAsync(t => t.CustomerId == req.CustomerId, ct);
        var recent = txns.OrderByDescending(t => t.CreatedAt).Take(req.Recent)
            .Select(t => new LoyaltyTxnDto(t.Points, t.Type, t.Description, t.CreatedAt)).ToList();
        return new CustomerLoyaltyDto(req.CustomerId, txns.Sum(t => t.Points), recent);
    }
}
