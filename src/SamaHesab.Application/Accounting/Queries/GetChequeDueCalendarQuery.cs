using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>
/// تقویمِ سررسیدِ چک‌های در جریان — سطل‌بندیِ زمانی (سررسیدگذشته/امروز/۷روز/۳۰روز/بعدتر)
/// + ریزِ روزانه، با جمعِ دریافتی/پرداختی و خالصِ نقدینگیِ هر بازه. گامِ بعدیِ خزانه در رودمپ.
/// </summary>
public record GetChequeDueCalendarQuery(string Today) : IRequest<ChequeDueCalendarResult>;

public class GetChequeDueCalendarQueryHandler : IRequestHandler<GetChequeDueCalendarQuery, ChequeDueCalendarResult>
{
    private readonly IChequeRepository _cheques;
    private readonly ICurrentUserService _currentUser;

    public GetChequeDueCalendarQueryHandler(IChequeRepository cheques, ICurrentUserService currentUser)
    {
        _cheques = cheques;
        _currentUser = currentUser;
    }

    public async Task<ChequeDueCalendarResult> Handle(GetChequeDueCalendarQuery request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var inProcess = await _cheques.GetByStatusAsync(companyId, ChequeStatus.InProcess, ct);

        var inputs = inProcess.Select(c =>
            new ChequeDueInput(c.DueDate, c.Amount, c.ChequeType == ChequeType.Received));

        return ChequeDueCalendar.Build(inputs, request.Today);
    }
}
