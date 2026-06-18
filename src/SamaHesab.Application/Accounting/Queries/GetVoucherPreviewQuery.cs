using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>پیش‌نمایشِ ردیف‌های یک سند (نامِ حساب + بدهکار/بستانکار) — منبعِ واحد (API + دسکتاپ).</summary>
public record VoucherPreviewLineDto(string AccountName, decimal Debit, decimal Credit);

public record GetVoucherPreviewQuery(int VoucherId) : IRequest<List<VoucherPreviewLineDto>>;

public class GetVoucherPreviewQueryHandler : IRequestHandler<GetVoucherPreviewQuery, List<VoucherPreviewLineDto>>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetVoucherPreviewQueryHandler(IVoucherRepository vouchers, IAccountRepository accounts, ICurrentUserService currentUser)
    { _vouchers = vouchers; _accounts = accounts; _currentUser = currentUser; }

    public async Task<List<VoucherPreviewLineDto>> Handle(GetVoucherPreviewQuery req, CancellationToken ct)
    {
        var v = await _vouchers.GetWithItemsAsync(req.VoucherId, ct);
        if (v == null) return new();
        var names = (await _accounts.GetLeafAccountsAsync(_currentUser.CompanyId ?? 1, ct))
            .ToDictionary(a => a.Id, a => a.Name);
        return v.Items.OrderBy(i => i.RowNumber)
            .Select(it => new VoucherPreviewLineDto(
                names.TryGetValue(it.AccountId, out var n) ? n : $"حساب {it.AccountId}",
                it.Debit, it.Credit))
            .ToList();
    }
}
