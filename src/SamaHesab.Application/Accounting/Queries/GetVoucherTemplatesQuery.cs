using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>فهرست الگوهای سند فعال شرکت (برای انتخاب سریع هنگام ثبت سند).</summary>
public record GetVoucherTemplatesQuery() : IRequest<List<VoucherTemplateDto>>;

public record VoucherTemplateDto(int Id, string Name, string? Description, int LineCount,
    decimal TotalDebit, decimal TotalCredit);

public class GetVoucherTemplatesQueryHandler : IRequestHandler<GetVoucherTemplatesQuery, List<VoucherTemplateDto>>
{
    private readonly IVoucherTemplateRepository _templates;
    private readonly ICurrentUserService _user;
    public GetVoucherTemplatesQueryHandler(IVoucherTemplateRepository templates, ICurrentUserService user)
    { _templates = templates; _user = user; }

    public async Task<List<VoucherTemplateDto>> Handle(GetVoucherTemplatesQuery request, CancellationToken ct)
    {
        var list = await _templates.GetByCompanyAsync(_user.CompanyId ?? 1, ct);
        return list
            .OrderBy(t => t.Name)
            .Select(t => new VoucherTemplateDto(t.Id, t.Name, t.Description, t.Lines.Count,
                t.TotalDebit, t.TotalCredit))
            .ToList();
    }
}
