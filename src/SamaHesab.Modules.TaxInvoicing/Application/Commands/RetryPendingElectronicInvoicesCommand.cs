using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.Commands;

/// <summary>
/// ارسالِ دستیِ صفِ Pending/Error (نه خودکار — تا فعالیتِ شبکه‌ای غافلگیرکننده نباشد، طبقِ پلن).
/// هر رکورد جداگانه از طریقِ <see cref="SendElectronicInvoiceCommand"/> پردازش می‌شود؛ شکستِ یکی
/// بقیه را متوقف نمی‌کند (هر رکورد خودش خطایش را نگه می‌دارد).
/// </summary>
public record RetryPendingElectronicInvoicesCommand(int MaxCount = 50) : IRequest<Result<RetrySummary>>;

public record RetrySummary(int Attempted, int Succeeded, int Failed);

public class RetryPendingElectronicInvoicesCommandHandler
    : IRequestHandler<RetryPendingElectronicInvoicesCommand, Result<RetrySummary>>
{
    private readonly IRepository<ElectronicInvoiceSubmission> _submissions;
    private readonly ICurrentUserService _user;
    private readonly IMediator _mediator;

    public RetryPendingElectronicInvoicesCommandHandler(
        IRepository<ElectronicInvoiceSubmission> submissions, ICurrentUserService user, IMediator mediator)
    { _submissions = submissions; _user = user; _mediator = mediator; }

    public async Task<Result<RetrySummary>> Handle(RetryPendingElectronicInvoicesCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var pending = (await _submissions.FindAsync(
            s => s.CompanyId == companyId && (s.Status == SubmissionStatus.Pending || s.Status == SubmissionStatus.Error), ct))
            .Take(req.MaxCount)
            .ToList();

        int ok = 0, fail = 0;
        foreach (var sub in pending)
        {
            var res = await _mediator.Send(new SendElectronicInvoiceCommand(sub.Id), ct);
            if (res.Succeeded) ok++; else fail++;
        }

        return Result<RetrySummary>.Success(new RetrySummary(pending.Count, ok, fail));
    }
}
