using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.API.Services;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Security.Commands;

namespace SamaHesab.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtTokenService _jwt;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;

    public AuthController(JwtTokenService jwt, ICurrentUserService currentUser, IMediator mediator)
    {
        _jwt = jwt;
        _currentUser = currentUser;
        _mediator = mediator;
    }

    public record LoginRequest(string Username, string Password, int CompanyId = 1, int BranchId = 1);

    /// <summary>Authenticate against Sec.Users (PBKDF2) and receive a JWT access + refresh token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenPair>> Login([FromBody] LoginRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _mediator.Send(new AuthenticateCommand(req.CompanyId, req.Username, req.Password, ip));
        if (!result.Succeeded || result.Value is null)
            return Unauthorized(new { message = result.ErrorMessage });

        var u = result.Value;
        return Ok(_jwt.Issue(new AuthenticatedUser(
            u.UserId, u.CompanyId, u.BranchId, u.Username, u.FullName, u.Roles)));
    }

    /// <summary>Return the current authenticated principal (smoke-test for JWT).</summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult Me() => Ok(new
    {
        _currentUser.UserId,
        _currentUser.CompanyId,
        _currentUser.BranchId,
        _currentUser.Username,
        _currentUser.FullName,
        Roles = _currentUser.GetRoles()
    });
}
