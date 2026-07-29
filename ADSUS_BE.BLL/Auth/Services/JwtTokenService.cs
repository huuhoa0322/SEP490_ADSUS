using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ADSUS_BE.BLL.Auth.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;

        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
        {
            throw new InvalidOperationException(
                "JwtSettings:SecretKey is missing. The key belongs in User Secrets — " +
                "right-click the ADSUS_BE project > Manage User Secrets. Ask the team for the shared key.");
        }
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.MobilePhone, user.Phone),
            new(ClaimTypes.Name, user.FullName),

            // Must be ClaimTypes.Role, otherwise [Authorize(Roles = "ADMIN")] never matches.
            new(ClaimTypes.Role, user.Role.ToApiString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
