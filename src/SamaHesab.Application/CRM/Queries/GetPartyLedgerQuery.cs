using MediatR;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>U-PARTY-LEDGER (backlog #9) — دفترِ معینِ طرف‌حساب به‌ترتیبِ زمانی، همراهِ ماندهٔ درحالِ‌گردش.</summary>
public record PartyLedgerRow(string Date, string DocType, string? DocNumber, string? Description,
    decimal Amount, decimal RunningBalance);

public record GetPartyLedgerQuery(int PartyId) : IRequest<List<PartyLedgerRow>>;

public class GetPartyLedgerQueryHandler : IRequestHandler<GetPartyLedgerQuery, List<PartyLedgerRow>>
{
    private readonly IRepository<PartyLedgerEntry> _ledger;
    public GetPartyLedgerQueryHandler(IRepository<PartyLedgerEntry> ledger) { _ledger = ledger; }

    public async Task<List<PartyLedgerRow>> Handle(GetPartyLedgerQuery req, CancellationToken ct)
    {
        var entries = (await _ledger.FindAsync(e => e.PartyId == req.PartyId, ct))
            .OrderBy(e => e.Date).ThenBy(e => e.Id).ToList();

        var rows = new List<PartyLedgerRow>();
        decimal running = 0;
        foreach (var e in entries)
        {
            running += e.Amount;
            rows.Add(new PartyLedgerRow(e.Date, e.DocType, e.DocNumber, e.Description, e.Amount, running));
        }
        return rows;
    }
}
