using System.Text;
using FluentValidation;

namespace GrocerEase.Application.Authentication.Validators;

internal static class PasswordRules
{
    // BCrypt only reads the first 72 bytes, so anything longer would be silently ignored.
    public const int MaxBytes = 72;

    // Counted in bytes, not characters: accented letters take two bytes and emoji four, so the limit can be reached
    // in fewer than 72 characters.
    public const string TooLongMessage =
        "Password must be at most 72 bytes in UTF-8; accented letters and emoji use more than one byte each.";

    public static IRuleBuilderOptions<T, string> FitsBcrypt<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(password => password is null || Encoding.UTF8.GetByteCount(password) <= MaxBytes)
            .WithMessage(TooLongMessage);
}
