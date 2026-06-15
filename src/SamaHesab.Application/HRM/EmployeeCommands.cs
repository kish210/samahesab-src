using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

// ── فهرستِ کارکنان ────────────────────────────────────────────────────────────
public record GetEmployeesQuery(bool IncludeInactive = false, string? Search = null) : IRequest<List<EmployeeDto>>;

public record EmployeeDto(int Id, string Code, string FirstName, string LastName, string FullName,
    string NationalCode, string? Mobile, decimal BaseSalary, string ContractType, bool IsActive);

public class GetEmployeesQueryHandler : IRequestHandler<GetEmployeesQuery, List<EmployeeDto>>
{
    private readonly IRepository<Employee> _repo;
    private readonly ICurrentUserService _user;
    public GetEmployeesQueryHandler(IRepository<Employee> repo, ICurrentUserService user)
    { _repo = repo; _user = user; }

    public async Task<List<EmployeeDto>> Handle(GetEmployeesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var all = await _repo.FindAsync(e => e.CompanyId == companyId && (req.IncludeInactive || e.IsActive), ct);
        return all
            .Where(e => string.IsNullOrWhiteSpace(req.Search)
                     || e.FullName.Contains(req.Search!) || e.Code.Contains(req.Search!) || e.NationalCode.Contains(req.Search!))
            .OrderBy(e => e.LastName)
            .Select(e => new EmployeeDto(e.Id, e.Code, e.FirstName, e.LastName, e.FullName, e.NationalCode, e.Mobile, e.BaseSalary, e.ContractType, e.IsActive))
            .ToList();
    }
}

// ── ذخیرهٔ کارمند (ایجاد/ویرایش) ────────────────────────────────────────────────
public record SaveEmployeeCommand(
    int Id, string Code, string NationalCode, string FirstName, string LastName,
    string HireDate, decimal BaseSalary, string ContractType,
    string? Mobile = null, string? Phone = null, string? Email = null, string? Address = null,
    string? FatherName = null, string? BirthDate = null, string? Gender = null, string? MaritalStatus = null,
    string? Education = null, int? DepartmentId = null, int? PositionId = null,
    string? BankName = null, string? BankAccount = null, string? ShebaNumber = null,
    string? InsuranceNumber = null, string? Notes = null) : IRequest<Result<int>>;

public class SaveEmployeeCommandValidator : AbstractValidator<SaveEmployeeCommand>
{
    public SaveEmployeeCommandValidator()
    {
        RuleFor(x => x.NationalCode).NotEmpty().WithMessage("کد ملی الزامی است.");
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("نام الزامی است.");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("نام خانوادگی الزامی است.");
        RuleFor(x => x.BaseSalary).GreaterThanOrEqualTo(0).WithMessage("حقوق پایه نمی‌تواند منفی باشد.");
    }
}

public class SaveEmployeeCommandHandler : IRequestHandler<SaveEmployeeCommand, Result<int>>
{
    private readonly IRepository<Employee> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveEmployeeCommandHandler(IRepository<Employee> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveEmployeeCommand r, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            Employee emp;
            if (r.Id > 0)
            {
                emp = await _repo.GetByIdAsync(r.Id, ct) ?? throw new InvalidOperationException("کارمند یافت نشد.");
                emp.UpdateSalary(r.BaseSalary);
            }
            else
            {
                var code = string.IsNullOrWhiteSpace(r.Code) ? "E" + DateTime.Now.ToString("yyMMddHHmm") : r.Code;
                emp = Employee.Create(companyId, _user.BranchId, code, r.NationalCode,
                    r.FirstName, r.LastName, r.HireDate, r.BaseSalary, r.ContractType);
                await _repo.AddAsync(emp, ct);
            }

            emp.UpdatePersonalInfo(r.FatherName, r.BirthDate, null, r.Gender, r.MaritalStatus, null, r.Education);
            emp.UpdateContactInfo(r.Mobile, r.Phone, r.Email, r.Address);
            emp.UpdateBankInfo(r.BankName, r.BankAccount, r.ShebaNumber);
            emp.UpdatePosition(r.DepartmentId, r.PositionId);

            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(emp.Id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}

// ── حذف (غیرفعال‌سازیِ نرم اگر تراکنش داشته باشد؛ این‌جا حذفِ ساده) ──────────────
public record DeleteEmployeeCommand(int Id) : IRequest<Result>;

public class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand, Result>
{
    private readonly IRepository<Employee> _repo;
    private readonly IUnitOfWork _uow;
    public DeleteEmployeeCommandHandler(IRepository<Employee> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(DeleteEmployeeCommand r, CancellationToken ct)
    {
        try
        {
            var emp = await _repo.GetByIdAsync(r.Id, ct);
            if (emp is null) return Result.Failure("کارمند یافت نشد.");
            _repo.Remove(emp);
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.GetBaseException().Message);
        }
    }
}
