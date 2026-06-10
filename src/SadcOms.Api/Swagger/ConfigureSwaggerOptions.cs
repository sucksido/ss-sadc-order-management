using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SadcOms.Api.Swagger;

/// <summary>Generates a Swagger document per discovered API version and adds the bearer scheme.</summary>
public sealed class ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = "SADC Order Management API",
                Version = description.ApiVersion.ToString(),
                Description = "Order management for the SADC region with currency validation, " +
                              "reliable messaging and ZAR FX reporting."
            });
        }

        var scheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste a JWT bearer token (obtain one from POST /api/dev/token in development).",
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        };

        options.AddSecurityDefinition("Bearer", scheme);
        options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
    }
}
