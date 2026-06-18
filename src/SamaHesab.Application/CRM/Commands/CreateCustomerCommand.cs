using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Commands;

/// <summary>ساختِ مشتری — مسیرِ نوشتنِ واحد (API + دسکتاپ). الگوی API-only.</summary>
public record CreateCustomerCommand(
    string Code, string CustomerType, string? FirstName, string? LastName, string? CompanyName,
    string? Phone, string? Mobile, string? Email, string? Province, string? City, string? Address, string? PostalCode,
    decimal CreditLimit, int CreditDays, string PriceLevel, decimal Discount,
    string? NationalCode, string? EconomicCode, int? GroupId, string? Notes,
    string? ContactPerson, string? Visitor, string? BirthDate) : IRequest<Result<int>>;

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<int>>
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Party> _parties;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateCustomerCommandHandler(IRepository<Customer> customers, IRepository<Party> parties,
        IUnitOfWork uow, ICurrentUserService currentUser)
    { _customers = customers; _parties = parties; _uow = uow; _currentUser = currentUser; }

    public async Task<Result<int>> Handle(CreateCustomerCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _currentUser.CompanyId ?? 1;
            var entity = Customer.Create(companyId, req.Code, req.CustomerType, req.FirstName, req.LastName, req.CompanyName);
            entity.UpdateContactInfo(req.Phone, req.Mobile, req.Email, req.Province, req.City, req.Address, req.PostalCode);
            entity.UpdateCreditTerms(req.CreditLimit, req.CreditDays, req.PriceLevel, req.Discount);
            entity.SetDetails(req.NationalCode, req.EconomicCode, req.GroupId, req.Notes);
            entity.SetContactPerson(req.ContactPerson, req.Visitor);
            if (!string.IsNullOrWhiteSpace(req.BirthDate)) entity.SetBirthDate(req.BirthDate!);

            await _customers.AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);

            // 🧱 dual-write به طرف‌حساب (Party) تا directoryِ اشخاص به‌روز بماند —
            // اگر شخصی با همین کد ملی هست، فقط نقشِ مشتری اضافه می‌شود؛ وگرنه طرف‌حسابِ جدید.
            try
            {
                Party? party = null;
                if (!string.IsNullOrWhiteSpace(req.NationalCode))
                    party = (await _parties.FindAsync(p => p.CompanyId == companyId && p.NationalCode == req.NationalCode, ct)).FirstOrDefault();
                if (party != null) { party.MarkCustomer(); party.SetLegacy(entity.Id, party.LegacySupplierId); _parties.Update(party); }
                else
                {
                    var np = Party.Create(companyId, req.Code, req.CustomerType, req.FirstName, req.LastName, req.CompanyName, isCustomer: true);
                    np.UpdateProfile(req.NationalCode, req.Mobile, req.Phone, req.Email, req.Province, req.City, req.Address);
                    np.SetLegacy(entity.Id, null);
                    await _parties.AddAsync(np, ct);
                }
                await _uow.SaveChangesAsync(ct);
            }
            catch { /* همگام‌سازیِ Party اختیاری است؛ نباید ساختِ مشتری را بشکند */ }

            return Result<int>.Success(entity.Id);
        }
        catch (System.Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
