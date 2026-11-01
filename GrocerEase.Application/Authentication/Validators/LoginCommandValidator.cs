using FluentValidation;
using GrocerEase.Application.Authentication.Commands;

namespace GrocerEase.Application.Authentication.Validators;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        // No password longer than this can have been registered, so there is nothing to hash and compare.
        RuleFor(x => x.Password)
            .NotEmpty()
            .FitsBcrypt();
    }
}
