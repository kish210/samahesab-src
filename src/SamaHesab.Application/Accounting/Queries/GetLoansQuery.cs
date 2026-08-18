using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

public record LoanInstallmentDto(int Index, decimal Payment, decimal Principal, decimal Interest, decimal Remaining);

public record LoanDto(
    int Id, string Code, string Name, string StartDate, decimal Principal,
    decimal AnnualInterestPercent, int TermMonths, int Status,
    int PaidInstallments, decimal PaidPrincipal, decimal PaidInterest,
    decimal RemainingPrincipal, string? LastPaymentDate, decimal MonthlyPayment);

public record GetLoansQuery() : IRequest<List<LoanDto>>;

public record GetLoanScheduleQuery(int Id) : IRequest<List<LoanInstallmentDto>>;

public class GetLoansQueryHandler : IRequestHandler<GetLoansQuery, List<LoanDto>>
{
    private readonly IRepository<Loan> _loans;
    private readonly ICurrentUserService _user;

    public GetLoansQueryHandler(IRepository<Loan> loans, ICurrentUserService user)
    { _loans = loans; _user = user; }

    public async Task<List<LoanDto>> Handle(GetLoansQuery request, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 0;
        var loans = await _loans.FindAsync(l => l.CompanyId == companyId, ct);

        return loans
            .OrderBy(l => l.Code)
            .Select(l =>
            {
                var payment = LoanCalculator.EqualPayment(l.Principal, l.AnnualInterestPercent, l.TermMonths);
                return new LoanDto(
                    l.Id, l.Code, l.Name, l.StartDate, l.Principal,
                    l.AnnualInterestPercent, l.TermMonths, (int)l.Status,
                    l.PaidInstallments, l.PaidPrincipal, l.PaidInterest,
                    l.RemainingPrincipal, l.LastPaymentDate, Math.Round(payment, 2));
            })
            .ToList();
    }
}

public class GetLoanScheduleQueryHandler : IRequestHandler<GetLoanScheduleQuery, List<LoanInstallmentDto>>
{
    private readonly IRepository<Loan> _loans;

    public GetLoanScheduleQueryHandler(IRepository<Loan> loans) { _loans = loans; }

    public async Task<List<LoanInstallmentDto>> Handle(GetLoanScheduleQuery request, CancellationToken ct)
    {
        var loan = await _loans.GetByIdAsync(request.Id, ct);
        if (loan is null) return new List<LoanInstallmentDto>();

        return LoanCalculator.BuildSchedule(loan.Principal, loan.AnnualInterestPercent, loan.TermMonths)
            .Select(i => new LoanInstallmentDto(i.Index, i.Payment, i.Principal, i.Interest, i.Remaining))
            .ToList();
    }
}
