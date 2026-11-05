using FluentValidation.TestHelper;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Validators;

namespace GrocerEase.UnitTests.Application;

public class RefreshTokenCommandValidatorTests
{
    private readonly RefreshTokenCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_HasNoErrors()
    {
        _validator.TestValidate(new RefreshTokenCommand(new string('a', 43))).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Token_Blank_IsRejected(string token)
    {
        _validator.TestValidate(new RefreshTokenCommand(token)).ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Token_AbsurdlyLong_IsRejectedBeforeAnythingHashesIt()
    {
        _validator.TestValidate(new RefreshTokenCommand(new string('a', 201))).ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}
