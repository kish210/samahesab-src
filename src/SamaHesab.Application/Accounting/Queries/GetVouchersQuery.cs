using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

public record GetVouchersQuery(
    int FiscalYearId,
    string? FromDate = null,
    string? ToDate = null,
    int? VoucherTypeId = null,
    int? Status = null,
    string? SearchText = null,
    int PageNumber = 1,
    int PageSize = 50
) : IRequest<PagedResult<VoucherListDto>>;

public record VoucherListDto(
    int Id,
    string VoucherNumber,
    string VoucherDate,
    string VoucherTypeName,
    string StatusName,
    decimal TotalDebit,
    decimal TotalCredit,
    string? Description,
    bool IsBalanced
);

public class GetVouchersQueryHandler : IRequestHandler<GetVouchersQuery, PagedResult<VoucherListDto>>
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly ICurrentUserService _currentUser;

    public GetVouchersQueryHandler(IVoucherRepository voucherRepository, ICurrentUserService currentUser)
    {
        _voucherRepository = voucherRepository;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<VoucherListDto>> Handle(GetVouchersQuery request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var fromDate = request.FromDate ?? "1400/01/01";
        var toDate = request.ToDate ?? "1410/12/29";

        var vouchers = await _voucherRepository.GetByDateRangeAsync(
            companyId, request.FiscalYearId, fromDate, toDate, ct);

        var statusMap = new Dictionary<int, string> { {1,"پیش‌نویس"},{2,"قطعی"},{3,"دائمی"} };
        var typeMap = new Dictionary<int, string> {
            {1,"افتتاحیه"},{2,"اختتامیه"},{3,"فروش"},{4,"خرید"},{5,"صندوق"},{6,"بانک"},
            {7,"چک"},{9,"عمومی"},{10,"پرداخت"},{11,"دریافت"},{12,"حقوق"} };

        var query = vouchers.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(v => (int)v.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchText))
            query = query.Where(v => v.VoucherNumber.Contains(request.SearchText)
                || (v.Description != null && v.Description.Contains(request.SearchText)));

        var total = query.Count();
        var items = query
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.VoucherNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(v => new VoucherListDto(
                v.Id,
                v.VoucherNumber,
                v.VoucherDate,
                typeMap.GetValueOrDefault(v.VoucherTypeId, v.VoucherTypeId.ToString()),
                statusMap.GetValueOrDefault((int)v.Status, "نامشخص"),
                v.Items.Sum(i => i.Debit),
                v.Items.Sum(i => i.Credit),
                v.Description,
                v.IsBalanced()
            ))
            .ToList();

        return PagedResult<VoucherListDto>.Create(items, total, request.PageNumber, request.PageSize);
    }
}
