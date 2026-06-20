using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>
/// UX-CRM-EDIT — بارگذاریِ فیلدهای قابلِ‌ویرایشِ یک مشتری برای فرمِ ویرایش.
/// (کارتِ ۳۶۰° نامِ ترکیبی می‌دهد؛ فرمِ ویرایش نام/نام‌خانوادگی/شرکت را جدا می‌خواهد.)
/// </summary>
public record GetCustomerForEditQuery(int CustomerId) : IRequest<CustomerEditDto?>;

public record CustomerEditDto(
    int Id, string Code, string CustomerType,
    string? FirstName, string? LastName, string? CompanyName,
    string? NationalCode, string? EconomicCode,
    string? Phone, string? Mobile, string? Email,
    string? Province, string? City, string? Address, string? PostalCode,
    decimal CreditLimit, int CreditDays, string PriceLevel, decimal Discount,
    string? Notes, string? ContactPerson, string? Visitor, bool IsActive);

public class GetCustomerForEditQueryHandler : IRequestHandler<GetCustomerForEditQuery, CustomerEditDto?>
{
    private readonly IRepository<Party> _parties;
    public GetCustomerForEditQueryHandler(IRepository<Party> parties) => _parties = parties;

    public async Task<CustomerEditDto?> Handle(GetCustomerForEditQuery req, CancellationToken ct)
    {
        var p = await _parties.GetByIdAsync(req.CustomerId, ct);
        if (p == null) return null;
        return new CustomerEditDto(
            p.Id, p.Code, p.PartyType,
            p.FirstName, p.LastName, p.CompanyName,
            p.NationalCode, p.EconomicCode,
            p.Phone, p.Mobile, p.Email,
            p.Province, p.City, p.Address, p.PostalCode,
            p.CreditLimit, p.CreditDays, p.PriceLevel, p.Discount,
            p.Notes, p.ContactPerson, p.Visitor, p.IsActive);
    }
}
