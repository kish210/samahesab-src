using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>فهرستِ چک‌ها — منبعِ واحد (API + دسکتاپ). الگوی API-only.</summary>
public record ChequeRowDto(int Id, ChequeType ChequeType, string ChequeNumber, string BankName,
    decimal Amount, string DueDate, ChequeStatus Status, string IssuedBy, string Description);

public record GetChequesQuery : IRequest<List<ChequeRowDto>>;

public class GetChequesQueryHandler : IRequestHandler<GetChequesQuery, List<ChequeRowDto>>
{
    private readonly IChequeRepository _cheques;
    private readonly ICurrentUserService _currentUser;
    public GetChequesQueryHandler(IChequeRepository cheques, ICurrentUserService currentUser)
    { _cheques = cheques; _currentUser = currentUser; }

    public async Task<List<ChequeRowDto>> Handle(GetChequesQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _cheques.FindAsync(c => c.CompanyId == companyId, ct);
        return list.Select(c => new ChequeRowDto(c.Id, c.ChequeType, c.ChequeNumber, c.BankName,
            c.Amount, c.DueDate, c.Status, c.IssuedBy ?? "", c.Description ?? "")).ToList();
    }
}
