using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.API.Services;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Security.Commands;
using SamaHesab.Application.Settings.Queries;

namespace SamaHesab.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtTokenService _jwt;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;
    private readonly RefreshTokenStore _refreshStore;
    private readonly IConfiguration _config;

    public AuthController(JwtTokenService jwt, ICurrentUserService currentUser, IMediator mediator,
        RefreshTokenStore refreshStore, IConfiguration config)
    {
        _jwt = jwt;
        _currentUser = currentUser;
        _mediator = mediator;
        _refreshStore = refreshStore;
        _config = config;
    }

    public record LoginRequest(string Username, string Password, int CompanyId = 1, int BranchId = 1);

    /// <summary>فهرستِ شرکت‌ها برایِ کمبویِ انتخابِ شرکت در صفحهٔ ورود — پیش از احرازِ هویت (فقط نام/کد، بدونِ دادهٔ حساس).</summary>
    [HttpGet("companies")]
    [AllowAnonymous]
    public async Task<IActionResult> Companies(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCompaniesQuery(), ct));
    public record RefreshRequest(string RefreshToken);

    private int RefreshDays => int.TryParse(_config["Jwt:RefreshTokenDays"], out var d) ? d : 14;

    private TokenPair IssueAndStore(AuthenticatedUser user)
    {
        var pair = _jwt.Issue(user);
        _refreshStore.Save(pair.RefreshToken, user, DateTime.UtcNow.AddDays(RefreshDays));
        return pair;
    }

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
        return Ok(IssueAndStore(new AuthenticatedUser(
            u.UserId, u.CompanyId, u.BranchId, u.Username, u.FullName, u.Roles, u.Permissions,
            u.SalespersonPartyId)));
    }

    /// <summary>Exchange a valid refresh token for a new access+refresh pair (rotation).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public ActionResult<TokenPair> Refresh([FromBody] RefreshRequest req)
    {
        if (!_refreshStore.TryConsume(req.RefreshToken, out var user) || user is null)
            return Unauthorized(new { message = "توکن تمدید نامعتبر یا منقضی است." });
        return Ok(IssueAndStore(user));
    }

    /// <summary>Revoke a refresh token (logout).</summary>
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout([FromBody] RefreshRequest req)
    {
        _refreshStore.Revoke(req.RefreshToken);
        return Ok(new { loggedOut = true });
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
        _currentUser.SalespersonPartyId,   // SP-1
        Roles = _currentUser.GetRoles(),
        Permissions = User.FindAll("perm").Select(c => c.Value).ToArray()
    });

    public record SetRecoveryCodeRequest(string RecoveryCode);

    /// <summary>ثبتِ کدِ بازیابی برایِ کاربرِ جاری (وب پیش‌تر هیچ راهی برایِ این نداشت — فقط
    /// ویزاردِ راه‌اندازیِ دسکتاپ که در وب اصلاً اجرا نمی‌شود). کد را خودِ کاربر تولید/یادداشت
    /// می‌کند؛ سرور فقط هشِ آن را نگه می‌دارد.</summary>
    [HttpPost("recovery-code")]
    [Authorize]
    public async Task<IActionResult> SetRecoveryCode([FromBody] SetRecoveryCodeRequest req)
    {
        var r = await _mediator.Send(new SetRecoveryCodeCommand(_currentUser.UserId!.Value, req.RecoveryCode));
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    public record ResetPasswordRecoveryRequest(string Username, string RecoveryCode, string NewPassword, int CompanyId = 1);

    /// <summary>بازنشانیِ رمزِ فراموش‌شده با کدِ بازیابی — بدونِ نیازِ لاگین (صفحهٔ ورود).</summary>
    [HttpPost("reset-password-recovery")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPasswordWithRecoveryCode([FromBody] ResetPasswordRecoveryRequest req)
    {
        var r = await _mediator.Send(new ResetPasswordWithRecoveryCodeCommand(
            req.CompanyId, req.Username, req.RecoveryCode, req.NewPassword));
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
