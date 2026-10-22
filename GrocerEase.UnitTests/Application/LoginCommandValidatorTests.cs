using FluentValidation.TestHelper;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Validators;

namespace GrocerEase.UnitTests.Application;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_HasNoErrors()
    {
        var result = _validator.TestValidate(new LoginCommand("ada@example.com", "x"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-email")]
    public void Email_MissingOrMalformed_IsRejected(string email)
    {
        var result = _validator.TestValidate(new LoginCommand(email, "Passw0rd!"));

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Password_Blank_IsRejected(string password)
    {
        var result = _validator.TestValidate(new LoginCommand("ada@example.com", password));

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}
