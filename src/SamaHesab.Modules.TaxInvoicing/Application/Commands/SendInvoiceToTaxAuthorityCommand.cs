using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.Commands;

/// <summary>
/// نقطهٔ ورودِ «ارسالِ دستی» از UIِ فاکتور (دکمهٔ روی هر ردیفِ لیستِ فاکتورهایِ فروش) — بر خلافِ
/// <see cref="SendElectronicInvoiceCommand"/> که به شناسهٔ رکوردِ صف‌شده (SubmissionId) نیاز دارد،
/// این دستور با همان شناسهٔ فاکتورِ فروش که UI از قبل دارد کار می‌کند: اگر رکوردِ ارسال برایِ این
/// فاکتور از قبل نبود (مثلاً چون هنگامِ قطعی‌شدنِ فاکتور ماژول هنوز فعال نبود)، همین‌جا Pending
/// ساخته می‌شود؛ سپس همان مسیرِ استانداردِ ارسال (<see cref="SendElectronicInvoiceCommand"/>) صدا زده می‌شود.
/// </summary>
public record SendInvoiceToTaxAuthorityCommand(int SalesInvoiceId) : IRequest<Result>;

public class SendInvoiceToTaxAuthorityCommandHandler : IRequestHandler<SendInvoiceToTaxAuthorityCommand, Result>
{
    private readonly IRepository<ElectronicInvoiceSubmission> _submissions;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly IMediator _mediator;

    public SendInvoiceToTaxAuthorityCommandHandler(IRepository<ElectronicInvoiceSubmission> submissions,
        IUnitOfWork uow, ICurrentUserService user, IMediator mediator)
    { _submissions = submissions; _uow = uow; _user = user; _mediator = mediator; }

    public async Task<Result> Handle(SendInvoiceToTaxAuthorityCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var sub = await _submissions.FindSingleAsync(
            s => s.CompanyId == companyId && s.SalesInvoiceId == req.SalesInvoiceId, ct);

        if (sub is null)
        {
            sub = ElectronicInvoiceSubmission.Create(companyId, req.SalesInvoiceId);
            await _submissions.AddAsync(sub, ct);
            await _uow.SaveChangesAsync(ct);
        }
        else if (sub.Status == SubmissionStatus.Accepted)
        {
            return Result.Failure("این فاکتور قبلاً به سامانهٔ مودیان ارسال و پذیرفته شده است.");
        }
        // Pending/Sent/Rejected/Error هر کدام باشد، دوباره تلاش می‌کنیم — SendElectronicInvoiceCommand
        // خودش وضعیتِ نهایی را بر اساسِ پاسخِ سازمان به‌روز می‌کند.

        return await _mediator.Send(new SendElectronicInvoiceCommand(sub.Id), ct);
    }
}
