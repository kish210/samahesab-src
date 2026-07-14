using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Application.Queries;

/// <summary>فهرستِ کالاها به‌همراهِ نگاشتِ (احتمالاً هنوز خالیِ) کدِ کالایِ مودیان — برایِ صفحهٔ
/// «کدهایِ کالاییِ مودیان». از <see cref="GetProductsQuery"/>ِ هستهٔ فروش (منبعِ واحدِ فهرستِ کالا)
/// به‌همراهِ رکوردهایِ <see cref="TaxItemCode"/>ِ همین ماژول تشکیل می‌شود.</summary>
public record GetTaxItemCodesQuery(string? Search = null) : IRequest<List<TaxItemCodeRowDto>>;

public record TaxItemCodeRowDto(int ProductId, string ProductCode, string ProductName,
    string? ItemId, string? MeasurementUnitCode);

public class GetTaxItemCodesQueryHandler : IRequestHandler<GetTaxItemCodesQuery, List<TaxItemCodeRowDto>>
{
    private readonly IRepository<TaxItemCode> _codes;
    private readonly ICurrentUserService _user;
    private readonly IMediator _mediator;

    public GetTaxItemCodesQueryHandler(IRepository<TaxItemCode> codes, ICurrentUserService user, IMediator mediator)
    { _codes = codes; _user = user; _mediator = mediator; }

    public async Task<List<TaxItemCodeRowDto>> Handle(GetTaxItemCodesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var products = await _mediator.Send(new GetProductsQuery(req.Search), ct);
        var codes = (await _codes.FindAsync(c => c.CompanyId == companyId, ct))
            .ToDictionary(c => c.ProductId);

        return products.Select(p => codes.TryGetValue(p.Id, out var c)
            ? new TaxItemCodeRowDto(p.Id, p.Code, p.Name, c.ItemId, c.MeasurementUnitCode)
            : new TaxItemCodeRowDto(p.Id, p.Code, p.Name, null, null)).ToList();
    }
}
