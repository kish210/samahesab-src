using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>U-FIN-NOTES — افزودنِ یادداشتِ توضیحی به یک صورتِ مالی.</summary>
public record AddFinancialStatementNoteCommand(
    FinancialStatementType StatementType, string Title, string? Body, int Order = 0) : IRequest<Result<int>>;

/// <summary>U-FIN-NOTES — ویرایشِ یادداشت (Idِ مسیر مرجع است).</summary>
public record UpdateFinancialStatementNoteCommand(
    int Id, string Title, string? Body, int Order) : IRequest<Result>;

/// <summary>U-FIN-NOTES — حذفِ یادداشت.</summary>
public record DeleteFinancialStatementNoteCommand(int Id) : IRequest<Result>;

public class AddFinancialStatementNoteCommandValidator : AbstractValidator<AddFinancialStatementNoteCommand>
{
    public AddFinancialStatementNoteCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("عنوانِ یادداشت الزامی است.").MaximumLength(200);
        RuleFor(x => x.Body).MaximumLength(2000);
    }
}

public class AddFinancialStatementNoteCommandHandler : IRequestHandler<AddFinancialStatementNoteCommand, Result<int>>
{
    private readonly IRepository<FinancialStatementNote> _notes;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public AddFinancialStatementNoteCommandHandler(IRepository<FinancialStatementNote> notes, IUnitOfWork uow, ICurrentUserService user)
    { _notes = notes; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(AddFinancialStatementNoteCommand req, CancellationToken ct)
    {
        try
        {
            var note = FinancialStatementNote.Create(_user.CompanyId!.Value, req.StatementType,
                req.Title, req.Body, req.Order);
            await _notes.AddAsync(note, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(note.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

public class UpdateFinancialStatementNoteCommandHandler : IRequestHandler<UpdateFinancialStatementNoteCommand, Result>
{
    private readonly IRepository<FinancialStatementNote> _notes;
    private readonly IUnitOfWork _uow;

    public UpdateFinancialStatementNoteCommandHandler(IRepository<FinancialStatementNote> notes, IUnitOfWork uow)
    { _notes = notes; _uow = uow; }

    public async Task<Result> Handle(UpdateFinancialStatementNoteCommand req, CancellationToken ct)
    {
        var note = await _notes.GetByIdAsync(req.Id, ct);
        if (note is null) return Result.Failure("یادداشت یافت نشد.");
        note.Update(req.Title, req.Body, req.Order);
        _notes.Update(note);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class DeleteFinancialStatementNoteCommandHandler : IRequestHandler<DeleteFinancialStatementNoteCommand, Result>
{
    private readonly IRepository<FinancialStatementNote> _notes;
    private readonly IUnitOfWork _uow;

    public DeleteFinancialStatementNoteCommandHandler(IRepository<FinancialStatementNote> notes, IUnitOfWork uow)
    { _notes = notes; _uow = uow; }

    public async Task<Result> Handle(DeleteFinancialStatementNoteCommand req, CancellationToken ct)
    {
        var note = await _notes.GetByIdAsync(req.Id, ct);
        if (note is null) return Result.Failure("یادداشت یافت نشد.");
        _notes.Remove(note);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
