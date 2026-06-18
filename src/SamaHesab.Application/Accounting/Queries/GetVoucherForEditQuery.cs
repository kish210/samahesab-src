using MediatR;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>بارگذاریِ سند برای ویرایش (هدر + ردیف‌ها) — منبعِ واحد (API + دسکتاپ).</summary>
public record VoucherEditItemDto(int RowNumber, int AccountId, string? Description, decimal Debit, decimal Credit);
public record VoucherEditDto(int Id, string VoucherNumber, string VoucherDate, int VoucherTypeId,
    string? Description, List<VoucherEditItemDto> Items);

public record GetVoucherForEditQuery(int VoucherId) : IRequest<VoucherEditDto?>;

public class GetVoucherForEditQueryHandler : IRequestHandler<GetVoucherForEditQuery, VoucherEditDto?>
{
    private readonly IVoucherRepository _vouchers;
    public GetVoucherForEditQueryHandler(IVoucherRepository vouchers) => _vouchers = vouchers;

    public async Task<VoucherEditDto?> Handle(GetVoucherForEditQuery req, CancellationToken ct)
    {
        var v = await _vouchers.GetWithItemsAsync(req.VoucherId, ct);
        if (v == null) return null;
        return new VoucherEditDto(v.Id, v.VoucherNumber, v.VoucherDate, v.VoucherTypeId, v.Description,
            v.Items.OrderBy(i => i.RowNumber)
                .Select(it => new VoucherEditItemDto(it.RowNumber, it.AccountId, it.Description, it.Debit, it.Credit))
                .ToList());
    }
}
