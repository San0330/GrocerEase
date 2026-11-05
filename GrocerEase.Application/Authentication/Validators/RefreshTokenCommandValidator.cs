using FluentValidation;
using GrocerEase.Application.Authentication.Commands;

namespace GrocerEase.Application.Authentication.Validators;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    // A real token is 43 characters. The limit only stops an enormous string from being hashed.
    public const int MaxLength = 200;

    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MaximumLength(MaxLength);
    }
}
