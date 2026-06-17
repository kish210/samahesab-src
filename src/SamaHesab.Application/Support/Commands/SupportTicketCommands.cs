using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Support.Commands;

/// <summary>🆘 HC-4 — ثبتِ تیکتِ پشتیبانی (محلی + همگام/صف).</summary>
public record CreateSupportTicketCommand(string Subject, string Body, SupportCategory Category)
    : IRequest<Result<BugReportResult>>;

public class CreateSupportTicketCommandHandler : IRequestHandler<CreateSupportTicketCommand, Result<BugReportResult>>
{
    private readonly IRepository<SupportTicket> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly ISupportApiClient _api;

    public CreateSupportTicketCommandHandler(IRepository<SupportTicket> repo, IUnitOfWork uow,
        ICurrentUserService user, ISupportApiClient api)
    { _repo = repo; _uow = uow; _user = user; _api = api; }

    public async Task<Result<BugReportResult>> Handle(CreateSupportTicketCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Subject)) return Result<BugReportResult>.Failure("موضوعِ تیکت الزامی است.");
        if (string.IsNullOrWhiteSpace(req.Body)) return Result<BugReportResult>.Failure("متنِ تیکت الزامی است.");

        var t = SupportTicket.Create(_user.CompanyId ?? 1, req.Subject, req.Body, req.Category);
        t.AddMessage(_user.FullName ?? _user.Username ?? "کاربر", req.Body, fromSupport: false);

        var message = "تیکت به‌صورتِ محلی ثبت شد و هنگامِ اتصال ارسال می‌شود.";
        if (_api.IsConfigured)
        {
            var sent = await _api.SubmitTicketAsync(new TicketSubmitDto(req.Subject, req.Body, (int)req.Category), ct);
            if (sent.Succeeded && sent.Value is { Length: > 0 })
            {
                t.MarkSynced(sent.Value);
                t.ChangeStatus(SupportStatus.Open);
                message = "تیکت با موفقیت ثبت شد. کدِ پیگیری: " + sent.Value;
            }
            else t.MarkQueued();
        }
        else t.MarkQueued();

        await _repo.AddAsync(t, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<BugReportResult>.Success(new BugReportResult(t.Id, t.Sync, message));
    }
}

/// <summary>🆘 HC-4 — افزودنِ پیام به یک تیکتِ موجود.</summary>
public record AddTicketMessageCommand(int TicketId, string Text) : IRequest<Result>;

public class AddTicketMessageCommandHandler : IRequestHandler<AddTicketMessageCommand, Result>
{
    private readonly IRepository<SupportTicket> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public AddTicketMessageCommandHandler(IRepository<SupportTicket> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result> Handle(AddTicketMessageCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Text)) return Result.Failure("متنِ پیام الزامی است.");
        var t = await _repo.GetByIdAsync(req.TicketId, ct);
        if (t is null) return Result.Failure("تیکت یافت نشد.");

        t.AddMessage(_user.FullName ?? _user.Username ?? "کاربر", req.Text, fromSupport: false);
        if (t.Status == SupportStatus.WaitingCustomer) t.ChangeStatus(SupportStatus.Open);
        _repo.Update(t);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
