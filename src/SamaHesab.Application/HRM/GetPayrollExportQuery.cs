using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// PAY-C1-4 — تولیدِ فایل‌های خروجیِ حقوقِ یک ماه از فیش‌های ذخیره‌شده (PAY-C1-3):
/// لیستِ بیمه · لیستِ مالیات · فایلِ بانک + خلاصهٔ پرداخت. منطقِ تولید در `PayrollExportService` (C2).
/// </summary>
public record GetPayrollExportQuery(string Year, byte Month, string WorkshopCode = "", string EmployerName = "")
    : IRequest<PayrollExportResult>;

public record PayrollExportResult(
    int EmployeeCount,
    decimal TotalNet, decimal TotalEmployeeInsurance, decimal TotalEmployerInsurance, decimal TotalTax,
    string InsuranceListCsv, string TaxListCsv, string BankFileCsv);

public class GetPayrollExportQueryHandler : IRequestHandler<GetPayrollExportQuery, PayrollExportResult>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<SalarySlip> _slips;
    private readonly ICurrentUserService _user;

    public GetPayrollExportQueryHandler(IRepository<Employee> employees, IRepository<SalarySlip> slips,
        ICurrentUserService user)
    { _employees = employees; _slips = slips; _user = user; }

    public async Task<PayrollExportResult> Handle(GetPayrollExportQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var emps = (await _employees.FindAsync(e => e.CompanyId == companyId, ct))
            .ToDictionary(e => e.Id, e => e);
        var empIds = emps.Keys.ToHashSet();

        // فیلترِ کارمندِ شرکت در خودِ کوئری (SalarySlip بدونِ CompanyId است).
        var slips = (await _slips.FindAsync(
                s => s.PeriodYear == req.Year && s.PeriodMonth == req.Month && empIds.Contains(s.EmployeeId), ct))
            .ToList();

        var rows = slips.Select(s =>
        {
            var e = emps[s.EmployeeId];
            // مشمولِ بیمه = ناخالص منهای حق اولاد (معاف از بیمه)؛ مشمولِ مالیات = ناخالص منهای بیمهٔ کارمند.
            var insurableBase = s.GrossSalary - s.ChildAllowance;
            var taxableIncome = s.GrossSalary - s.InsuranceDeduct;
            return new PayrollExportRow(
                EmployeeName: e.FullName,
                NationalCode: e.NationalCode ?? "",
                InsuranceNo: e.InsuranceNumber ?? "",
                Sheba: e.ShebaNumber ?? "",
                WorkDays: 30,
                InsurableBase: insurableBase,
                EmployeeInsurance: s.InsuranceDeduct,
                EmployerInsurance: s.EmployerInsurance,
                Tax: s.TaxDeduct,
                TaxableIncome: taxableIncome,
                NetPay: s.NetSalary);
        }).ToList();

        var period = new PayrollPeriod(SafeInt(req.Year), req.Month, req.WorkshopCode, req.EmployerName);

        return new PayrollExportResult(
            EmployeeCount: rows.Count,
            TotalNet: rows.Sum(r => r.NetPay),
            TotalEmployeeInsurance: rows.Sum(r => r.EmployeeInsurance),
            TotalEmployerInsurance: rows.Sum(r => r.EmployerInsurance),
            TotalTax: rows.Sum(r => r.Tax),
            InsuranceListCsv: PayrollExportService.BuildInsuranceList(period, rows),
            TaxListCsv: PayrollExportService.BuildTaxList(period, rows),
            BankFileCsv: PayrollExportService.BuildBankFile(rows));
    }

    private static int SafeInt(string s) => int.TryParse(s, out var v) ? v : 0;
}
