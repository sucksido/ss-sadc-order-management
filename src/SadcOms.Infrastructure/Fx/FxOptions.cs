namespace SadcOms.Infrastructure.Fx;

public sealed class FxOptions
{
    public const string SectionName = "Fx";

    /// <summary>How long a fetched rate is cached before we refresh it.</summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);
}
