using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SadcOms.Api.Security;

namespace SadcOms.Api.Controllers;

/// <summary>
/// Issues short-lived development JWTs so the API can be exercised locally (Swagger/Postman)
/// without a real Microsoft Entra tenant. Disabled automatically once an Entra authority is
/// configured — production never mints its own tokens.
/// </summary>
[ApiController]
[AllowAnonymous]
[ApiVersion("1.0")]
[Route("api/dev")]
public sealed class DevTokenController(IOptions<JwtOptions> jwtOptions, IWebHostEnvironment env) : ControllerBase
{
    public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<TokenResponse> IssueToken([FromQuery] string subject = "local-developer")
    {
        var options = jwtOptions.Value;

        // Only available in non-production hosts that use the symmetric dev key.
        if (options.UseEntra || env.IsProduction() || string.IsNullOrWhiteSpace(options.DevSigningKey))
        {
            return NotFound();
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.DevSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddHours(1);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("scope", "orders.read orders.write")
            ],
            expires: expires,
            signingCredentials: credentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new TokenResponse(jwt, "Bearer", (int)TimeSpan.FromHours(1).TotalSeconds));
    }
}
