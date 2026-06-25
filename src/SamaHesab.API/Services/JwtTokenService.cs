using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SamaHesab.API.Services;

public record AuthenticatedUser(int UserId, int CompanyId, int BranchId, string Username, string FullName,
    string[] Roles, string[] Permissions, int? SalespersonPartyId = null);
public record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAt);

public class JwtTokenService
{
    private readonly IConfiguration _config;
    public JwtTokenService(IConfiguration config) => _config = config;

    public TokenPair Issue(AuthenticatedUser user)
    {
        var jwt = _config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var minutes = int.TryParse(jwt["AccessTokenMinutes"], out var m) ? m : 60;
        var expires = DateTime.UtcNow.AddMinutes(minutes);

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
            issuer: jwt["Issuer"], audience: jwt["Audience"],
            claims: claims, expires: expires, signingCredentials: creds);

        var access = new JwtSecurityTokenHandler().WriteToken(token);
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new TokenPair(access, refresh, expires);
    }
}
