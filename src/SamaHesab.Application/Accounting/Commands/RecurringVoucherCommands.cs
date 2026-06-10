using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

// ── تعریف یک سند تکرارشونده ───────────────────────────────────────────────────
public record SaveRecurringVoucherCommand(int TemplateId, string Name,
    RecurrenceFrequency Frequency, string StartDate) : IRequest<Result<int>>;

public class SaveRecurringVoucherCommandValidator : AbstractValidator<SaveRecurringVoucherCommand>
{
    public SaveRecurringVoucherCommandValidator()
    {
        RuleFor(x => x.TemplateId).GreaterThan(0).WithMessage("الگو الزامی است.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام الزامی است.");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("تاریخ شروع الزامی است.");
    }
}

public class SaveRecurringVoucherCommandHandler : IRequestHandler<SaveRecurringVoucherCommand, Result<int>>
{
    private readonly IRecurringVoucherRepository _recurring;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveRecurringVoucherCommandHandler(IRecurringVoucherRepository recurring, IUnitOfWork uow, ICurrentUserService user)
    { _recurring = recurring; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveRecurringVoucherCommand req, CancellationToken ct)
    {
        try
        {
            var entity = RecurringVoucher.Create(_user.CompanyId ?? 1, _user.BranchId ?? 1,
                req.TemplateId, req.Name, (int)req.Frequency, req.StartDate);
            await _recurring.AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(entity.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── موتور تولید: تمام اسناد سررسیدشده را به‌صورت پیش‌نویس می‌سازد ───────────────
public record GenerateDueRecurringVouchersCommand(string Today) : IRequest<Result<RecurringRunResult>>;
public record RecurringRunResult(int Generated, List<int> VoucherIds);

public class GenerateDueRecurringVouchersCommandHandler
    : IRequestHandler<GenerateDueRecurringVouchersCommand, Result<RecurringRunResult>>
{
    private readonly IRecurringVoucherRepository _recurring;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public GenerateDueRecurringVouchersCommandHandler(IRecurringVoucherRepository recurring,
        IMediator mediator, IUnitOfWork uow, ICurrentUserService user)
    { _recurring = recurring; _mediator = mediator; _uow = uow; _user = user; }

    public async Task<Result<RecurringRunResult>> Handle(GenerateDueRecurringVouchersCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            var actives = await _recurring.GetActiveAsync(companyId, ct);
            var createdIds = new List<int>();

            foreach (var rv in actives)
            {
                // ممکن است یک سند چند دوره عقب باشد؛ تا رسیدن به امروز تولید کن.
                var guard = 0;
                while (RecurrenceSchedule.IsDue(rv.NextDate, req.Today) && guard++ < 240)
                {
                    var dueDate = rv.NextDate;
                    var created = await _mediator.Send(
                        new CreateVoucherFromTemplateCommand(rv.TemplateId, dueDate, rv.Name), ct);
                    if (!created.Succeeded) break;   // الگو ناموجود/خطا → این مورد را رها کن

                    createdIds.Add(created.Value);
                    var next = RecurrenceSchedule.NextAfter(dueDate, (RecurrenceFrequency)rv.Frequency);
                    rv.MarkGenerated(dueDate, next);
                    _recurring.Update(rv);
                    await _uow.SaveChangesAsync(ct);
                }
            }

            return Result<RecurringRunResult>.Success(new RecurringRunResult(createdIds.Count, createdIds));
        }
        catch (Exception ex) { return Result<RecurringRunResult>.Failure(ex.GetBaseException().Message); }
    }
}
