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
}
