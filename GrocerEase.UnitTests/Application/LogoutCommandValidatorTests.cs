using FluentValidation.TestHelper;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Validators;

namespace GrocerEase.UnitTests.Application;

public class LogoutCommandValidatorTests
{
    private readonly LogoutCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_HasNoErrors()
    {
        _validator.TestValidate(new LogoutCommand(new string('a', 43))).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Token_Blank_IsRejected(string token)
    {
        _validator.TestValidate(new LogoutCommand(token)).ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Token_AbsurdlyLong_IsRejected()
    {
        _validator.TestValidate(new LogoutCommand(new string('a', 201))).ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}
