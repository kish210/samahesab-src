using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Commands;

/// <summary>
/// UX-CRM-EDIT — به‌روزرسانیِ مشتریِ موجود (نه ساختِ تکراری). آینهٔ فیلدهای CreateCustomerCommand.
/// </summary>
public record UpdateCustomerCommand(
    int Id, string CustomerType, string? FirstName, string? LastName, string? CompanyName,
    string? Phone, string? Mobile, string? Email, string? Province, string? City, string? Address, string? PostalCode,
    decimal CreditLimit, int CreditDays, string PriceLevel, decimal Discount,
    string? NationalCode, string? EconomicCode, string? Notes,
    string? ContactPerson, string? Visitor, int? GroupId = null, string? BirthDate = null,
    // چندوجهی‌بودنِ ماهیتِ شخص — با تیک‌زدن در فرم؛ null یعنی «دست‌نخورده بماند» (سازگاریِ عقب‌رو با فراخوان‌های قدیمی).
    bool? IsCustomerRole = null, bool? IsSupplierRole = null, bool? IsEmployeeRole = null, bool? IsSalespersonRole = null)
    : IRequest<Result<int>>;

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        // فرمِ ویرایش همیشه هر چهار چک‌باکس را می‌فرستد (هیچ‌کدام null نیست) — آنگاه دست‌کم یکی باید تیک بخورد.
        RuleFor(x => x).Must(x =>
                x.IsCustomerRole is null && x.IsSupplierRole is null && x.IsEmployeeRole is null && x.IsSalespersonRole is null
                || x.IsCustomerRole == true || x.IsSupplierRole == true || x.IsEmployeeRole == true || x.IsSalespersonRole == true)
            .WithMessage("دست‌کم یک ماهیت (خریدار/تأمین‌کننده/کارمند/فروشنده) باید تیک بخورد.");
    }
}

public class UpdateCustomerCommandHandler : IRequestHandler<UpdateCustomerCommand, Result<int>>
{
    private readonly IRepository<Party> _parties;
    private readonly IUnitOfWork _uow;
    public UpdateCustomerCommandHandler(IRepository<Party> parties, IUnitOfWork uow)
    { _parties = parties; _uow = uow; }

    public async Task<Result<int>> Handle(UpdateCustomerCommand req, CancellationToken ct)
    {
        try
        {
            var p = await _parties.GetByIdAsync(req.Id, ct);
            if (p == null) return Result<int>.Failure("مشتری یافت نشد.");

            p.EditCore(req.CustomerType, req.FirstName, req.LastName, req.CompanyName,
                req.PostalCode, req.ContactPerson, req.Visitor, req.GroupId, req.BirthDate);
            p.UpdateProfile(req.NationalCode, req.Mobile, req.Phone, req.Email, req.Province, req.City, req.Address);
            p.SetTaxIds(req.NationalCode, req.EconomicCode, req.Notes);
            p.SetCreditTerms(req.CreditLimit, req.CreditDays, req.PriceLevel, req.Discount);
            // فرمِ ویرایش همیشه هر چهار چک‌باکس را می‌فرستد؛ اگر ارسال نشده (فراخوانِ قدیمی)، نقش‌های فعلی حفظ می‌شوند.
            if (req.IsCustomerRole is not null || req.IsSupplierRole is not null
                || req.IsEmployeeRole is not null || req.IsSalespersonRole is not null)
                p.SetRoles(req.IsCustomerRole ?? p.IsCustomer, req.IsSupplierRole ?? p.IsSupplier,
                    req.IsEmployeeRole ?? p.IsEmployee, req.IsSalespersonRole ?? p.IsSalesperson);

            _parties.Update(p);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(p.Id);
        }
        catch (System.Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
