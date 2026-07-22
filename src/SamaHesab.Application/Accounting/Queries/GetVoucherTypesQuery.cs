using MediatR;
using SamaHesab.Application.Accounting;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>فهرستِ نوعِ سند برایِ کمبویِ فیلتر/فرمِ ثبت — بدونِ جدولِ اختصاصی، فقط پورتِ `VoucherTypeCatalog`.</summary>
public record VoucherTypeDto(int Id, string Name);

public record GetVoucherTypesQuery() : IRequest<List<VoucherTypeDto>>;

public class GetVoucherTypesQueryHandler : IRequestHandler<GetVoucherTypesQuery, List<VoucherTypeDto>>
{
    public Task<List<VoucherTypeDto>> Handle(GetVoucherTypesQuery request, CancellationToken ct)
        => Task.FromResult(VoucherTypeCatalog.Names
            .Select(kv => new VoucherTypeDto(kv.Key, kv.Value))
            .OrderBy(t => t.Id)
            .ToList());
}
