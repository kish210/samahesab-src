using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>
/// U-BANK-RECON-WEB — اجرایِ مغایرت‌گیری بانکی: دفترِ بانکِ بازه را بارگذاری، ردیف‌های
/// ازقبل‌تطبیق‌شده را کنار می‌گذارد، صورت‌حسابِ CSV را پارس و به‌صورت خودکار تطبیق می‌دهد.
/// </summary>
public record RunBankReconciliationQuery(int BankAccountId, string FromDate, string ToDate, string StatementCsv)
    : IRequest<BankReconciliationResult>;

public record BankReconciliationMatchDto(int VoucherItemId, string Date, decimal Amount, string Description, string Reference);
public record BankReconciliationResult(
    string BankName,
    int MatchedCount,
    int UnmatchedLedgerCount,
    int UnmatchedStatementCount,
    int AlreadyReconciledCount,
    string? LastReconciledDate,
    List<BankReconciliationMatchDto> Matched,
    List<BankLedgerLineDto> UnmatchedLedger,
    List<StatementLine> UnmatchedStatement);

public class RunBankReconciliationQueryHandler : IRequestHandler<RunBankReconciliationQuery, BankReconciliationResult>
{
    private readonly IMediator _mediator;
    private readonly IRepository<BankReconciledItem> _reconciled;
    private readonly ICurrentUserService _user;

    public RunBankReconciliationQueryHandler(IMediator mediator,
        IRepository<BankReconciledItem> reconciled, ICurrentUserService user)
    { _mediator = mediator; _reconciled = reconciled; _user = user; }

    public async Task<BankReconciliationResult> Handle(RunBankReconciliationQuery req, CancellationToken ct)
    {
        var ledgerResult = await _mediator.Send(new GetBankLedgerQuery(req.BankAccountId, req.FromDate, req.ToDate), ct);
        if (ledgerResult.GlAccountId == 0)
            return new BankReconciliationResult(ledgerResult.BankName, 0, 0, 0, 0, null, new(), new(), new());

        var companyId = _user.CompanyId ?? 1;
        var already = await _reconciled.FindAsync(
            x => x.BankAccountId == req.BankAccountId && x.CompanyId == companyId, ct);
        var alreadyIds = new HashSet<int>(already.Select(x => x.VoucherItemId));
        var lastDate = already.OrderByDescending(x => x.ReconciledDate).FirstOrDefault()?.ReconciledDate;

        var open = ledgerResult.Lines.Where(l => !alreadyIds.Contains(l.VoucherItemId)).ToList();

        var statement = BankStatementParser.Parse(req.StatementCsv);
        var recon = BankReconciliation.AutoMatch(
            open.Select(l => new LedgerLine(l.VoucherItemId, l.Date, l.Amount)),
            statement);

        var byId = open.ToDictionary(l => l.VoucherItemId);
        var matched = recon.Matched
            .Select(m =>
            {
                var desc = byId.TryGetValue(m.Ledger.VoucherItemId, out var dto) ? dto.Description : "";
                return new BankReconciliationMatchDto(m.Ledger.VoucherItemId, m.Ledger.Date, m.Ledger.Amount, desc, m.Statement.Reference ?? "");
            })
            .ToList();

        var unmatchedLedger = recon.UnmatchedLedger
            .Select(l => byId.TryGetValue(l.VoucherItemId, out var dto)
                ? dto : new BankLedgerLineDto(l.VoucherItemId, l.Date, l.Amount, ""))
            .ToList();

        return new BankReconciliationResult(
            ledgerResult.BankName,
            matched.Count,
            unmatchedLedger.Count,
            recon.UnmatchedStatement.Count,
            alreadyIds.Count,
            lastDate,
            matched,
            unmatchedLedger,
            recon.UnmatchedStatement.ToList());
    }
}
