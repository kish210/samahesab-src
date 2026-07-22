using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.HRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-HR — پرسنل (دادهٔ پایهٔ هسته، مستقل از ماژولِ HR). CQRSِ کاملی از قبل در
/// Application/HRM/EmployeeCommands.cs بود ولی هیچ endpointی صدایش نمی‌زد؛ کلاینتِ وب
/// هیچ صفحهٔ کارمندی نداشت.
/// </summary>
[ApiController]
[Authorize]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IRepository<Employee> _employees;

    public EmployeesController(IMediator mediator, IRepository<Employee> employees)
    { _mediator = mediator; _employees = employees; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive, [FromQuery] string? search,
        [FromQuery] int? branchId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetEmployeesQuery(includeInactive, search, branchId), ct));

    /// <summary>جزئیاتِ کامل برای فرمِ ویرایش — لیست فقط ستون‌های خلاصه دارد.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var e = await _employees.GetByIdAsync(id, ct);
        if (e is null) return NotFound();
        return Ok(new
        {
            e.Id, e.Code, e.NationalCode, e.FirstName, e.LastName, e.FatherName, e.BirthDate,
            e.Gender, e.MaritalStatus, e.Education, e.Mobile, e.Phone, e.Email, e.Address,
            e.DepartmentId, e.PositionId, e.HireDate, e.ContractType, e.BaseSalary,
            e.BankName, e.BankAccount, e.ShebaNumber, e.InsuranceNumber, e.ChildrenCount,
            e.IsActive, e.Notes,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveEmployeeCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { Id = 0 }, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] SaveEmployeeCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { Id = id }, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteEmployeeCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
