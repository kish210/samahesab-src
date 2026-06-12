using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Security.Commands;

namespace SamaHesab.API.Controllers;

/// <summary>مدیریت امنیت/RBAC: نقش‌ها، مجوزها، تخصیص نقش به کاربر.</summary>
[ApiController]
[Route("api/security")]
[Authorize]
public class SecurityController : ControllerBase
{
    private readonly IMediator _mediator;
    public SecurityController(IMediator mediator) => _mediator = mediator;

    [HttpGet("permissions")]
    public async Task<IActionResult> Catalog() => Ok(await _mediator.Send(new GetPermissionCatalogQuery()));

    [HttpGet("roles")]
    public async Task<IActionResult> Roles() => Ok(await _mediator.Send(new GetRolesQuery()));

    [HttpPost("roles")]
    public async Task<IActionResult> SaveRole([FromBody] SaveRoleCommand cmd)
    {
        var r = await _mediator.Send(cmd);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    public record RolePermsBody(string[] Codes);
    [HttpPut("roles/{id:int}/permissions")]
    public async Task<IActionResult> SetPermissions(int id, [FromBody] RolePermsBody body)
    {
        var r = await _mediator.Send(new SetRolePermissionsCommand(id, body.Codes ?? Array.Empty<string>()));
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users() => Ok(await _mediator.Send(new GetSecurityUsersQuery()));

    public record UserRolesBody(int[] RoleIds);
    [HttpPut("users/{id:int}/roles")]
    public async Task<IActionResult> SetUserRoles(int id, [FromBody] UserRolesBody body)
    {
        var r = await _mediator.Send(new SetUserRolesCommand(id, body.RoleIds ?? Array.Empty<int>()));
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
