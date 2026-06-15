using MediatR;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>T22 — کارتابلِ تأیید: اسنادِ «در انتظارِ تأیید» (جدیدترین اول).</summary>
public record GetPendingApprovalsQuery() : IRequest<List<PendingApprovalDto>>;

public record PendingApprovalDto(int Id, string VoucherNumber, string VoucherDate, string? Description, decimal TotalDebit);

public class GetPendingApprovalsQueryHandler : IRequestHandler<GetPendingApprovalsQuery, List<PendingApprovalDto>>
{
    private readonly IVoucherRepository _vouchers;
    public GetPendingApprovalsQueryHandler(IVoucherRepository vouchers) => _vouchers = vouchers;

    public async Task<List<PendingApprovalDto>> Handle(GetPendingApprovalsQuery req, CancellationToken ct)
    {
        // فیلترِ شرکت/شعبه به‌صورتِ سراسری در DbContext اعمال می‌شود.
        var rows = await _vouchers.FindAsync(v => v.ApprovalStatus == VoucherApprovalStatus.PendingApproval, ct);
        return rows
            .OrderByDescending(v => v.VoucherDate).ThenByDescending(v => v.Id)
            .Select(v => new PendingApprovalDto(v.Id, v.VoucherNumber, v.VoucherDate, v.Description, v.TotalDebit))
            .ToList();
    }
}
