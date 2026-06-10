namespace SadcOms.Api.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Microsoft Entra authority, e.g. https://login.microsoftonline.com/{tenant}/v2.0.
    /// When set, tokens are validated against Entra's published metadata (production path).
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>Expected audience (the API's Application ID URI / client id).</summary>
    public string Audience { get; set; } = "sadc-oms-api";

    /// <summary>Issuer used for locally-minted development tokens.</summary>
    public string Issuer { get; set; } = "sadc-oms-dev";

    /// <summary>
    /// Symmetric signing key for development tokens. Only used when <see cref="Authority"/> is
    /// not configured. Never set this in production — real deployments use Entra.
    /// </summary>
    public string? DevSigningKey { get; set; }

    public bool UseEntra => !string.IsNullOrWhiteSpace(Authority);
}
