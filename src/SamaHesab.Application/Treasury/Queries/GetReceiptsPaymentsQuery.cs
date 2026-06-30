using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Treasury.Queries;

/// <summary>یک ردیفِ فهرستِ دریافت/پرداخت (سندِ نوع ۱۱=دریافت / ۱۰=پرداخت).</summary>
public record ReceiptPaymentRow(int Id, string Kind, string VoucherNumber, string Date, decimal Amount, string? Description);

/// <summary>
/// فهرستِ اخیرِ دریافت‌ها و پرداخت‌های خزانه (سندهای نوع ۱۱/۱۰) برای صفحهٔ «دریافت و پرداختِ وجه».
/// </summary>
public record GetReceiptsPaymentsQuery(int FiscalYearId, string? FromDate = null, string? ToDate = null, int Take = 50)
    : IRequest<IReadOnlyList<ReceiptPaymentRow>>;

public class GetReceiptsPaymentsQueryHandler : IRequestHandler<GetReceiptsPaymentsQuery, IReadOnlyList<ReceiptPaymentRow>>
{
    private readonly IVoucherRepository _vouchers;
    private readonly ICurrentUserService _user;

    public GetReceiptsPaymentsQueryHandler(IVoucherRepository vouchers, ICurrentUserService user)
    { _vouchers = vouchers; _user = user; }

    public async Task<IReadOnlyList<ReceiptPaymentRow>> Handle(GetReceiptsPaymentsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var all = await _vouchers.GetByDateRangeAsync(companyId, req.FiscalYearId,
            req.FromDate ?? "1400/01/01", req.ToDate ?? "1410/12/29", ct);

        return all
            .Where(v => v.VoucherTypeId == 11 || v.VoucherTypeId == 10)   // ۱۱=دریافت · ۱۰=پرداخت
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.VoucherNumber)
            .Take(req.Take)
            .Select(v => new ReceiptPaymentRow(
                v.Id,
                v.VoucherTypeId == 11 ? "دریافت" : "پرداخت",
                v.VoucherNumber, v.VoucherDate,
                v.Items.Sum(i => i.Debit),
                v.Description))
            .ToList();
    }
}
