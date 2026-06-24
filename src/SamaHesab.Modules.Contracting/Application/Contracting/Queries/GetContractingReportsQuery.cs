using MediatR;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Modules.Contracting.Domain;

namespace SamaHesab.Modules.Contracting.Application.Queries;

/// <summary>
/// CON-C2-6 — گزارش‌های پیمانکاری. کوئری‌های تست‌شدهٔ موجود (داشبوردِ هر پیمان + ضمانت‌نامه‌ها) را
/// از طریقِ MediatR فرا می‌خواند و با <see cref="ContractingReportBuilder"/> به سه
/// <see cref="ReportTable"/>ِ آماده تبدیل می‌کند (خلاصهٔ مالیِ پیمان‌ها · سپرده‌های نگه‌داشته · دفترِ ضمانت‌نامه‌ها).
/// هم‌الگوی <c>GetTourismReportsQuery</c>.
/// </summary>
public record GetContractingReportsQuery() : IRequest<ContractingReportsDto>;

public record ContractingReportsDto(
    ReportTable FinancialSummary,
    ReportTable DepositsHeld,
    ReportTable Guarantees);

public class GetContractingReportsQueryHandler : IRequestHandler<GetContractingReportsQuery, ContractingReportsDto>
{
    private readonly IMediator _mediator;
    public GetContractingReportsQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<ContractingReportsDto> Handle(GetContractingReportsQuery req, CancellationToken ct)
    {
        var projects = await _mediator.Send(new GetContractProjectsQuery(), ct);
        var titles = projects.ToDictionary(p => p.Id, p => p.Title);

        var summaryRows = new List<ProjectSummaryRow>();
        var depositRows = new List<DepositRow>();
        foreach (var p in projects)
        {
            var d = await _mediator.Send(new GetProjectDashboardQuery(p.Id), ct);
            if (d is null) continue;
            summaryRows.Add(new ProjectSummaryRow(d.Code, d.Title, d.ContractAmount, d.CumulativeBilled,
                d.RetentionHeld, d.InsuranceHeld, d.AdvanceOutstanding, d.NetBilled));
            depositRows.Add(new DepositRow(d.Title, d.RetentionHeld, d.InsuranceHeld));
        }

        var guarantees = await _mediator.Send(new GetGuaranteesQuery(null, ActiveOnly: false), ct);
        var guaranteeRows = guarantees.Select(g => new GuaranteeRow(
            titles.GetValueOrDefault(g.ContractProjectId, $"#{g.ContractProjectId}"),
            TypeLabel(g.Type), g.Bank, g.Amount, g.ExpiryDate, g.DaysToExpiry, StatusLabel(g.Status)));

        return new ContractingReportsDto(
            ContractingReportBuilder.ProjectFinancialSummary(summaryRows),
            ContractingReportBuilder.DepositsHeld(depositRows),
            ContractingReportBuilder.GuaranteeRegister(guaranteeRows));
    }

    private static string TypeLabel(GuaranteeType t) => t switch
    {
        GuaranteeType.Advance => "پیش‌پرداخت",
        GuaranteeType.Performance => "حسن‌انجام‌کار",
        GuaranteeType.Tender => "شرکت در مناقصه",
        _ => t.ToString()
    };

    private static string StatusLabel(GuaranteeStatus s) => s switch
    {
        GuaranteeStatus.Active => "فعال",
        GuaranteeStatus.Released => "آزادشده",
        GuaranteeStatus.Expired => "منقضی",
        _ => s.ToString()
    };
}
