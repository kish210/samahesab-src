using MediatR;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Settings.Queries;

/// <summary>U-MULTI-COMPANY-1 — لیستِ شرکت‌هایِ فعال برایِ کمبویِ انتخابِ شرکت در صفحهٔ ورود.
/// پیش‌تر این کوئری اصلاً وجود نداشت؛ LoginViewModel یک شرکتِ هاردکدشده نشان می‌داد.</summary>
public record GetCompaniesQuery : IRequest<List<CompanyListItemDto>>;

public record CompanyListItemDto(int Id, string Code, string Name);

public class GetCompaniesQueryHandler : IRequestHandler<GetCompaniesQuery, List<CompanyListItemDto>>
{
    private readonly IRepository<Company> _companies;
    public GetCompaniesQueryHandler(IRepository<Company> companies) => _companies = companies;

    public async Task<List<CompanyListItemDto>> Handle(GetCompaniesQuery req, CancellationToken ct)
    {
        var rows = await _companies.FindAsync(c => c.IsActive, ct);
        return rows.OrderBy(c => c.Id)
            .Select(c => new CompanyListItemDto(c.Id, c.Code, c.Name))
            .ToList();
    }
}
