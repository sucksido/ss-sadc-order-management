using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace SadcOms.Api.Security;

public static class AuthenticationSetup
{
    public const string ApiScopePolicy = "ApiAccess";

    /// <summary>
    /// Configures JWT bearer auth. With an Entra authority configured it validates tokens
    /// against Entra (production). Otherwise it falls back to a symmetric dev key so the API
    /// can be exercised locally with tokens minted by the dev-token endpoint.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, JwtOptions options)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                if (options.UseEntra)
                {
                    jwt.Authority = options.Authority;
                    jwt.Audience = options.Audience;
                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidAudience = options.Audience,
                        ValidateLifetime = true
                    };
                }
                else
                {
                    var key = options.DevSigningKey
                              ?? throw new InvalidOperationException("Jwt:DevSigningKey is required when no Entra Authority is configured.");

                    jwt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = options.Issuer,
                        ValidateAudience = true,
                        ValidAudience = options.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                }
            });

        services.AddAuthorization(authz =>
        {
            // A baseline policy requiring an authenticated principal. In production this would
            // also assert a scope/role claim issued by Entra (e.g. "orders.write").
            authz.AddPolicy(ApiScopePolicy, policy => policy.RequireAuthenticatedUser());
            authz.DefaultPolicy = authz.GetPolicy(ApiScopePolicy)!;
        });

        return services;
    }
}
