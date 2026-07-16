using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SamaHesab.API.Services;

public record AuthenticatedUser(int UserId, int CompanyId, int BranchId, string Username, string FullName,
    string[] Roles, string[] Permissions, int? SalespersonPartyId = null);
public record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAt);

/// <summary>
/// کلیدِ مؤثرِ امضایِ JWT — از Program.cs (پس از اعمالِ override برایِ Productionِ بدونِ کلیدِ واقعی؛
/// SR-3/SR-4) محاسبه و به‌عنوانِ singleton ثبت می‌شود. اگر JwtTokenService به‌جایِ این، مستقیم از
/// IConfiguration کلیدِ خام (پیش از override) را بخواند، توکنِ صادرشده با کلیدِ متفاوتی از
/// کلیدِ اعتبارسنجیِ AddJwtBearer امضا می‌شود و هر لاگین در آن حالت با ۴۰۱ رد می‌شود (باگِ واقعی
/// که در تستِ زندهٔ کلاینتِ وب کشف شد).
/// </summary>
public record JwtSettings(string Key, string? Issuer, string? Audience, int AccessTokenMinutes);

public class JwtTokenService
{
    private readonly JwtSettings _settings;
    public JwtTokenService(JwtSettings settings) => _settings = settings;

    public TokenPair Issue(AuthenticatedUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new("uid", user.UserId.ToString()),
            new("companyId", user.CompanyId.ToString()),
            new("branchId", user.BranchId.ToString()),
            new("username", user.Username),
            new("fullName", user.FullName),
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (user.SalespersonPartyId is > 0)
            claims.Add(new Claim("seller", user.SalespersonPartyId.Value.ToString()));   // SP-1
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(user.Permissions.Select(p => new Claim("perm", p)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer, audience: _settings.Audience,
            claims: claims, expires: expires, signingCredentials: creds);

        var access = new JwtSecurityTokenHandler().WriteToken(token);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new TokenPair(access, refresh, expires);
    }
}
