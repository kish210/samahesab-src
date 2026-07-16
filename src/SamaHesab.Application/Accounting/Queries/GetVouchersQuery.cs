using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Security;
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
    bool IsBalanced,
    string UserName = "—"
);

public class GetVouchersQueryHandler : IRequestHandler<GetVouchersQuery, PagedResult<VoucherListDto>>
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<User> _users;

    public GetVouchersQueryHandler(IVoucherRepository voucherRepository, ICurrentUserService currentUser,
        IRepository<User> users)
    {
        _voucherRepository = voucherRepository;
        _currentUser = currentUser;
        _users = users;
    }

    /// <summary>U-DB-PAGING (@2026-07-16) — پیش‌تر کلِ بازهٔ تاریخ بارگذاری و Skip/Take در حافظه
    /// اعمال می‌شد؛ حالا صفحه‌بندی واقعاً DB-level (OFFSET/FETCH) است.</summary>
    public async Task<PagedResult<VoucherListDto>> Handle(GetVouchersQuery request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var fromDate = request.FromDate ?? "1400/01/01";
        var toDate = request.ToDate ?? "1410/12/29";

        var statusMap = new Dictionary<int, string> { {1,"پیش‌نویس"},{2,"قطعی"},{3,"دائمی"} };
        var typeMap = new Dictionary<int, string> {
            {1,"افتتاحیه"},{2,"اختتامیه"},{3,"فروش"},{4,"خرید"},{5,"صندوق"},{6,"بانک"},
            {7,"چک"},{9,"عمومی"},{10,"پرداخت"},{11,"دریافت"},{12,"حقوق"} };

        var (paged, total) = await _voucherRepository.GetPagedByDateRangeAsync(
            companyId, request.FiscalYearId, fromDate, toDate,
            request.Status, request.SearchText, request.PageNumber, request.PageSize, ct);

        // نام کاربرِ ثبت‌کننده (CreatedByUserId → نام) — «—» اگر ثبت نشده باشد.
        var userIds = paged.Where(v => v.CreatedByUserId.HasValue).Select(v => v.CreatedByUserId!.Value).Distinct().ToList();
        var userNames = userIds.Count > 0
            ? (await _users.FindAsync(u => userIds.Contains(u.Id), ct)).ToDictionary(u => u.Id, u => u.FullName)
            : new Dictionary<int, string>();

        var items = paged
            .Select(v => new VoucherListDto(
                v.Id,
                v.VoucherNumber,
                v.VoucherDate,
                typeMap.GetValueOrDefault(v.VoucherTypeId, v.VoucherTypeId.ToString()),
                statusMap.GetValueOrDefault((int)v.Status, "نامشخص"),
                v.Items.Sum(i => i.Debit),
                v.Items.Sum(i => i.Credit),
                v.Description,
                v.IsBalanced(),
                v.CreatedByUserId.HasValue && userNames.TryGetValue(v.CreatedByUserId.Value, out var n) ? n : "—"
            ))
            .ToList();

        return PagedResult<VoucherListDto>.Create(items, total, request.PageNumber, request.PageSize);
    }
}
