using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>
/// تابلوی چک‌های در جریان، مرتب بر اساس سررسید (نزدیک‌ترین اول) با علامت سررسیدگذشته/امروز —
/// گردش‌کار پیگیری وصول/پرداخت چک.
/// </summary>
public record GetChequeBoardQuery(string Today) : IRequest<List<ChequeBoardDto>>;

public record ChequeBoardDto(
    int Id,
    string ChequeNumber,
    string BankName,
    decimal Amount,
    string DueDate,
    string Type,        // دریافتی / پرداختی
    string DueState);   // Overdue / DueToday / Upcoming

public class GetChequeBoardQueryHandler : IRequestHandler<GetChequeBoardQuery, List<ChequeBoardDto>>
{
    private readonly IChequeRepository _cheques;
    private readonly ICurrentUserService _currentUser;

    public GetChequeBoardQueryHandler(IChequeRepository cheques, ICurrentUserService currentUser)
    {
        _cheques = cheques;
        _currentUser = currentUser;
    }

    public async Task<List<ChequeBoardDto>> Handle(GetChequeBoardQuery request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var inProcess = await _cheques.GetByStatusAsync(companyId, ChequeStatus.InProcess, ct);

        return inProcess
            .OrderBy(c => c.DueDate)
            .Select(c => new ChequeBoardDto(
                c.Id, c.ChequeNumber, c.BankName, c.Amount, c.DueDate,
                Type: c.ChequeType == ChequeType.Received ? "دریافتی" : "پرداختی",
                DueState: ChequeBoard.Classify(c.DueDate, request.Today).ToString()))
            .ToList();
    }
}
