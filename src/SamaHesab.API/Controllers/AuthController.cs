using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.API.Services;
using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtTokenService _jwt;
    private readonly ICurrentUserService _currentUser;

    public AuthController(JwtTokenService jwt, ICurrentUserService currentUser)
    {
        _jwt = jwt;
        _currentUser = currentUser;
    }

    public record LoginRequest(string Username, string Password, int CompanyId = 1, int BranchId = 1);

    /// <summary>Authenticate and receive a JWT access + refresh token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<TokenPair> Login([FromBody] LoginRequest req)
    {
        // NOTE: mirrors the desktop client's credential check for now. A
        // production deployment validates against Sec.Users (hashed passwords).
        var ok = req.Username == "admin" && (req.Password == "admin123" || req.Password == "1234");
        if (!ok) return Unauthorized(new { message = "نام کاربری یا رمز عبور نادرست است." });

        var user = new AuthenticatedUser(
            UserId: 1, CompanyId: req.CompanyId, BranchId: req.BranchId,
            Username: req.Username, FullName: "مدیر سیستم", Roles: new[] { "ADMIN" });

        return Ok(_jwt.Issue(user));
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
