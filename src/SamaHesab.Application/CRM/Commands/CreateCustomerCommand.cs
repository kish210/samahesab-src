using FluentValidation;
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

/// <summary>
/// اعتبارسنجیِ مشتری (رفعِ باگ: قبلاً هیچ validationی نبود → نام‌های نامعتبر مثل «jhkj» ثبت می‌شد).
/// نامِ معتبر الزامی است: برای «حقوقی» نامِ شرکت، برای «حقیقی» نام/نام‌خانوادگی — دستِ‌کم ۲ نویسه.
/// </summary>
public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("کدِ مشتری الزامی است.");
        RuleFor(x => x).Must(HasValidName)
            .WithMessage("نامِ معتبرِ مشتری را وارد کنید (برای حقوقی «نامِ شرکت» و برای حقیقی «نام»، دستِ‌کم ۲ نویسه).");
    }

    private static bool HasValidName(CreateCustomerCommand c)
    {
        var isLegal = string.Equals(c.CustomerType?.Trim(), "حقوقی", System.StringComparison.Ordinal);
        var name = (isLegal ? c.CompanyName : $"{c.FirstName} {c.LastName}")?.Trim() ?? string.Empty;
        return name.Length >= 2;
    }
}

public class CreateCustomerCommandHandler : IRequestHandler<CreateCustomerCommand, Result<int>>
{
    private readonly IRepository<Party> _parties;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CreateCustomerCommandHandler(IRepository<Party> parties, IUnitOfWork uow, ICurrentUserService currentUser)
    { _parties = parties; _uow = uow; _currentUser = currentUser; }

    public async Task<Result<int>> Handle(CreateCustomerCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _currentUser.CompanyId ?? 1;

            // 🧱 طرف‌حسابِ یکپارچه: اگر شخصی با همین کد ملی هست، فقط نقشِ مشتری اضافه می‌شود؛ وگرنه طرف‌حسابِ جدید.
            Party? party = null;
            if (!string.IsNullOrWhiteSpace(req.NationalCode))
                party = (await _parties.FindAsync(p => p.CompanyId == companyId && p.NationalCode == req.NationalCode, ct)).FirstOrDefault();

            if (party != null)
            {
                party.MarkCustomer();
                party.EditCore(req.CustomerType, req.FirstName, req.LastName, req.CompanyName,
                    req.PostalCode, req.ContactPerson, req.Visitor, req.GroupId, req.BirthDate);
                party.UpdateProfile(req.NationalCode, req.Mobile, req.Phone, req.Email, req.Province, req.City, req.Address);
                party.SetTaxIds(req.NationalCode, req.EconomicCode, req.Notes);
                party.SetCreditTerms(req.CreditLimit, req.CreditDays, req.PriceLevel, req.Discount);
                _parties.Update(party);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(party.Id);
            }

            var np = Party.Create(companyId, req.Code, req.CustomerType, req.FirstName, req.LastName, req.CompanyName, isCustomer: true);
            np.SetBranch(_currentUser.BranchId);   // P3 — طرف‌حسابِ نو با شعبهٔ سازنده تگ می‌شود (null اگر شعبه نامشخص = مشترک)
            // همهٔ فیلدهای فرم باید ذخیره شوند (کدپستی/کدِ اقتصادی/یادداشت/رابط/ویزیتور قبلاً می‌افتادند).
            np.EditCore(req.CustomerType, req.FirstName, req.LastName, req.CompanyName,
                req.PostalCode, req.ContactPerson, req.Visitor, req.GroupId, req.BirthDate);
            np.UpdateProfile(req.NationalCode, req.Mobile, req.Phone, req.Email, req.Province, req.City, req.Address);
            np.SetTaxIds(req.NationalCode, req.EconomicCode, req.Notes);
            np.SetCreditTerms(req.CreditLimit, req.CreditDays, req.PriceLevel, req.Discount);
            await _parties.AddAsync(np, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(np.Id);
        }
        catch (System.Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
