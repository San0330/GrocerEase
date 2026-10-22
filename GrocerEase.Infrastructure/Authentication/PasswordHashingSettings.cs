using System.ComponentModel.DataAnnotations;

namespace GrocerEase.Infrastructure.Authentication;

public class PasswordHashingSettings
{
    public const string SectionName = "PasswordHashing";

    [Range(4, 31)]
    public int Cost { get; init; } = 11;
}
