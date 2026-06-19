using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>کارت ۳۶۰° مشتری — شناسنامه + چکِ در جریان. منبعِ واحد (API + دسکتاپ).
/// اعتبار/تحلیل/گردش از کوئری‌های موجود (GetCustomerCredit/Analytics/Statement) جدا گرفته می‌شوند.</summary>
public record CustomerCardDto(
    int Id, string Name, string Code, string CustomerType, string PriceLevel,
    string? Mobile, string? Phone, string? NationalCode, string? EconomicCode,
    string? ContactPerson, string? Visitor, string? Province, string? City, string? Address,
    int LoyaltyPoints, int CreditDays, bool IsActive, decimal Balance, decimal CreditLimit,
    decimal ChequeInProgress);

public record GetCustomerCardQuery(int CustomerId) : IRequest<CustomerCardDto?>;

public class GetCustomerCardQueryHandler : IRequestHandler<GetCustomerCardQuery, CustomerCardDto?>
{
    private readonly IRepository<Party> _customers;
    private readonly IRepository<Cheque> _cheques;
    public GetCustomerCardQueryHandler(IRepository<Party> customers, IRepository<Cheque> cheques)
    { _customers = customers; _cheques = cheques; }

    public async Task<CustomerCardDto?> Handle(GetCustomerCardQuery req, CancellationToken ct)
    {
        var c = await _customers.GetByIdAsync(req.CustomerId, ct);
        if (c == null) return null;

        var inProc = await _cheques.FindAsync(ch => ch.PartyId == req.CustomerId
            && ch.ChequeType == ChequeType.Received
            && ch.Status == ChequeStatus.InProcess, ct);

        return new CustomerCardDto(c.Id, c.FullName, c.Code, c.PartyType, c.PriceLevel,
            c.Mobile, c.Phone, c.NationalCode, c.EconomicCode, c.ContactPerson, c.Visitor,
            c.Province, c.City, c.Address, c.LoyaltyPoints, c.CreditDays, c.IsActive,
            c.Balance, c.CreditLimit, inProc.Sum(x => x.Amount));
    }
}
