using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Contracting.Domain;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Contracting.Application.Queries;

/// <summary>فهرستِ پیمان‌ها (برای انتخاب‌گر و پیش‌نمایشِ زندهٔ آبشار در UIِ صورت‌وضعیت — CON-C2-2).</summary>
public record GetContractProjectsQuery(bool ActiveOnly = false) : IRequest<List<ContractProjectListDto>>;

public record ContractProjectListDto(
    int Id, string Code, string Title, int EmployerPartyId, string EmployerName,
    decimal ContractAmount, decimal AdvancePercent, decimal RetentionPercent,
    decimal InsurancePercent, decimal TaxPercent, decimal AdvanceOutstanding, string Status);

public class GetContractProjectsQueryHandler : IRequestHandler<GetContractProjectsQuery, List<ContractProjectListDto>>
{
    private readonly IRepository<ContractProject> _projects;
    private readonly IRepository<AdvancePayment> _advances;
    private readonly IRepository<Party> _parties;
    private readonly ICurrentUserService _user;

    public GetContractProjectsQueryHandler(IRepository<ContractProject> projects, IRepository<AdvancePayment> advances,
        IRepository<Party> parties, ICurrentUserService user)
    { _projects = projects; _advances = advances; _parties = parties; _user = user; }

    public async Task<List<ContractProjectListDto>> Handle(GetContractProjectsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var projects = await _projects.FindAsync(
            p => p.CompanyId == companyId && (!req.ActiveOnly || p.Status == ContractProjectStatus.Active), ct);

        var advByProject = (await _advances.FindAsync(a => a.CompanyId == companyId, ct))
            .GroupBy(a => a.ContractProjectId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Outstanding));

        var employerIds = projects.Select(p => p.EmployerPartyId).ToHashSet();
        var names = (await _parties.FindAsync(p => employerIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id, p => p.FullName);

        return projects
            .OrderByDescending(p => p.Id)
            .Select(p => new ContractProjectListDto(
                p.Id, p.Code, p.Title, p.EmployerPartyId, names.GetValueOrDefault(p.EmployerPartyId, $"#{p.EmployerPartyId}"),
                p.ContractAmount, p.AdvancePercent, p.RetentionPercent, p.InsuranceWithholdPercent, p.TaxWithholdPercent,
                advByProject.GetValueOrDefault(p.Id, 0m), p.Status.ToString()))
            .ToList();
    }
}
