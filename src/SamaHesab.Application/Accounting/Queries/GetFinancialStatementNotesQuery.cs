using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

public record FinancialStatementNoteDto(int Id, int StatementType, string Title, string? Body, int Order);

public record GetFinancialStatementNotesQuery(FinancialStatementType StatementType)
    : IRequest<List<FinancialStatementNoteDto>>;

public class GetFinancialStatementNotesQueryHandler
    : IRequestHandler<GetFinancialStatementNotesQuery, List<FinancialStatementNoteDto>>
{
    private readonly IRepository<FinancialStatementNote> _notes;
    private readonly ICurrentUserService _user;

    public GetFinancialStatementNotesQueryHandler(IRepository<FinancialStatementNote> notes, ICurrentUserService user)
    { _notes = notes; _user = user; }

    public async Task<List<FinancialStatementNoteDto>> Handle(GetFinancialStatementNotesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 0;
        var notes = await _notes.FindAsync(
            n => n.CompanyId == companyId && n.StatementType == req.StatementType, ct);

        return notes
            .OrderBy(n => n.Order).ThenBy(n => n.Id)
            .Select(n => new FinancialStatementNoteDto(n.Id, (int)n.StatementType, n.Title, n.Body, n.Order))
            .ToList();
    }
}
