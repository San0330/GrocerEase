using System.ComponentModel.DataAnnotations;

namespace GrocerEase.Api.RateLimiting;

public class AuthRateLimitSettings
{
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>Register and login attempts one client address may make in a window.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; init; } = 10;

    [Range(1, 86_400)]
    public int WindowSeconds { get; init; } = 60;
}
