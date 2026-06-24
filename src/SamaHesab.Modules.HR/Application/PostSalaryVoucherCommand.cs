using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// M7 فاز۲ — سندِ حسابداریِ خودکارِ پرداختِ حقوقِ یک ماه:
///   بدهکار «هزینهٔ حقوق» (۸-۰۱-۰۰۱) = ناخالصِ کل
///   بستانکار «حقوق پرداختنی» (۳-۰۵-۰۰۱) = خالصِ کل
///   بستانکار «بیمهٔ پرداختنی» (۳-۰۵-۰۰۲) = جمعِ بیمه
///   بستانکار «مالیات پرداختنی» (۳-۰۴-۰۰۲) = جمعِ مالیات
/// (ناخالص = خالص + بیمه + مالیات → سند متوازن.) حقوق از همان `PayrollCalculator` محاسبه می‌شود.
/// </summary>
public record PostSalaryVoucherCommand(string Date) : IRequest<Result<PostSalaryResult>>;

public record PostSalaryResult(int VoucherId, int EmployeeCount, decimal Gross, decimal Net);

public class PostSalaryVoucherCommandHandler : IRequestHandler<PostSalaryVoucherCommand, Result<PostSalaryResult>>
{
    private const string ExpenseCode = "8-01-001";   // هزینهٔ حقوق
    private const string SalaryPayable = "3-05-001";  // حقوق پرداختنی
    private const string InsurancePayable = "3-05-002"; // بیمهٔ پرداختنی
    private const string TaxPayable = "3-04-002";     // مالیات پرداختنی
    private const int GeneralVoucherTypeId = 9;        // GEN/عمومی

    private readonly IRepository<Employee> _employees;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public PostSalaryVoucherCommandHandler(IRepository<Employee> employees, IAccountRepository accounts,
        IVoucherRepository vouchers, IRepository<FiscalYear> fiscalYears, IUnitOfWork uow, ICurrentUserService user)
    {
        _employees = employees; _accounts = accounts; _vouchers = vouchers;
        _fiscalYears = fiscalYears; _uow = uow; _user = user;
    }

    public async Task<Result<PostSalaryResult>> Handle(PostSalaryVoucherCommand req, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _user.CompanyId ?? 1;
            var emps = await _employees.FindAsync(e => e.CompanyId == companyId && e.IsActive, ct);
            if (emps.Count == 0) return Fail("کارمندِ فعالی برای صدورِ حقوق نیست.");

            decimal gross = 0, insurance = 0, tax = 0, net = 0;
            foreach (var e in emps)
            {
                var r = PayrollCalculator.Compute(new PayrollInput(e.BaseSalary));
                gross += r.Gross; insurance += r.Insurance; tax += r.Tax; net += r.Net;
            }
            if (gross <= 0) return Fail("جمعِ حقوق صفر است.");

            var fy = await _fiscalYears.FindSingleAsync(f => f.CompanyId == companyId && f.IsActive && !f.IsClosed, ct);
            if (fy is null) return Fail("سال مالیِ فعالی برای ثبتِ سند نیست.");

            var expense = await _accounts.GetByCodeAsync(companyId, ExpenseCode, ct);
            var payable = await _accounts.GetByCodeAsync(companyId, SalaryPayable, ct);
            var insAcc = await _accounts.GetByCodeAsync(companyId, InsurancePayable, ct);
            var taxAcc = await _accounts.GetByCodeAsync(companyId, TaxPayable, ct);
            if (expense is null || payable is null || insAcc is null || taxAcc is null)
                return Fail("حساب‌های حقوق/بیمه/مالیات تعریف نشده‌اند (مهاجرتِ ۲۸ را اجرا کنید).");

            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, _user.BranchId ?? 1, fy.Id, number, req.Date,
                GeneralVoucherTypeId, $"حقوقِ {emps.Count} نفر — {req.Date}", $"PAYROLL-{number}");
            int row = 1;
            v.AddItem(VoucherItem.Create(0, row++, expense.Id, gross, 0, "هزینهٔ حقوق و دستمزد"));
            v.AddItem(VoucherItem.Create(0, row++, payable.Id, 0, net, "خالصِ پرداختنی به کارکنان"));
            if (insurance > 0) v.AddItem(VoucherItem.Create(0, row++, insAcc.Id, 0, insurance, "بیمهٔ سهمِ کارمند"));
            if (tax > 0) v.AddItem(VoucherItem.Create(0, row++, taxAcc.Id, 0, tax, "مالیاتِ حقوق"));
            v.Post(_user.UserId ?? 0);
            await _vouchers.AddAsync(v, ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<PostSalaryResult>.Success(new PostSalaryResult(v.Id, emps.Count, gross, net));
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Fail(ex.GetBaseException().Message);
        }
    }

    private static Result<PostSalaryResult> Fail(string m) => Result<PostSalaryResult>.Failure(m);
}
