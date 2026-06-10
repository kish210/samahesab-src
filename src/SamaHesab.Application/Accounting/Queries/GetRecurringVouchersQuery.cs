using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>فهرست اسناد تکرارشونده‌ی فعال شرکت با تاریخ سررسید بعدی.</summary>
public record GetRecurringVouchersQuery() : IRequest<List<RecurringVoucherDto>>;

public record RecurringVoucherDto(int Id, int TemplateId, string Name, string Frequency,
    string NextDate, string? LastGeneratedDate);

public class GetRecurringVouchersQueryHandler : IRequestHandler<GetRecurringVouchersQuery, List<RecurringVoucherDto>>
{
    private readonly IRecurringVoucherRepository _recurring;
    private readonly ICurrentUserService _user;
    public GetRecurringVouchersQueryHandler(IRecurringVoucherRepository recurring, ICurrentUserService user)
    { _recurring = recurring; _user = user; }

    private static string Fa(int f) => f == 1 ? "سالانه" : "ماهانه";

    public async Task<List<RecurringVoucherDto>> Handle(GetRecurringVouchersQuery request, CancellationToken ct)
    {
        var list = await _recurring.GetActiveAsync(_user.CompanyId ?? 1, ct);
        return list
            .OrderBy(r => r.NextDate)
            .Select(r => new RecurringVoucherDto(r.Id, r.TemplateId, r.Name, Fa(r.Frequency),
                r.NextDate, r.LastGeneratedDate))
            .ToList();
    }
}
