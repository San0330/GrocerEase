using System.ComponentModel.DataAnnotations;

namespace GrocerEase.Infrastructure.Authentication;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    [Required]
    [MinLength(32)]    
    public string Secret { get; init; } = null!;

    [Required]
    public string Issuer { get; init; } = null!;

    [Required]
    public string Audience { get; init; } = null!;

    [Range(1, int.MaxValue)]
    public int ExpiryMinutes { get; init; }

    /// <summary>How long a refresh token works, counted from when it is issued. Each refresh issues a new one.</summary>
    [Range(1, int.MaxValue)]
    public int RefreshTokenExpiryDays { get; init; } = 30;
}
