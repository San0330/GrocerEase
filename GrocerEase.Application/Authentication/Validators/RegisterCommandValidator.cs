using System.Text;
using FluentValidation;
using GrocerEase.Application.Authentication.Commands;

namespace GrocerEase.Application.Authentication.Validators;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    // BCrypt only reads the first 72 bytes, so anything longer would be silently ignored.
    private const int MaxPasswordBytes = 72;

    public RegisterCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(254);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Must(password => password is null || Encoding.UTF8.GetByteCount(password) <= MaxPasswordBytes)
            .WithMessage("Password must be at most 72 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}
