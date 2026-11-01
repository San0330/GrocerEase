using FluentValidation.TestHelper;
using GrocerEase.Application.Authentication.Commands;
using GrocerEase.Application.Authentication.Validators;

namespace GrocerEase.UnitTests.Application;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand Valid() =>
        new("Ada", "Lovelace", "ada@example.com", "Passw0rd!");

    [Fact]
    public void ValidCommand_HasNoErrors()
    {
        var result = _validator.TestValidate(Valid());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void FirstName_Blank_IsRejected(string firstName)
    {
        var result = _validator.TestValidate(Valid() with { FirstName = firstName });

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void FirstName_LongerThan50_IsRejected()
    {
        var result = _validator.TestValidate(Valid() with { FirstName = new string('A', 51) });

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void FirstName_At50Characters_IsAccepted()
    {
        var result = _validator.TestValidate(Valid() with { FirstName = new string('A', 50) });

        result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void LastName_Blank_IsRejected(string lastName)
    {
        var result = _validator.TestValidate(Valid() with { LastName = lastName });

        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void LastName_LongerThan50_IsRejected()
    {
        var result = _validator.TestValidate(Valid() with { LastName = new string('B', 51) });

        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-an-email")]
    public void Email_MissingOrMalformed_IsRejected(string email)
    {
        var result = _validator.TestValidate(Valid() with { Email = email });

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_LongerThan254_IsRejected()
    {
        var result = _validator.TestValidate(Valid() with { Email = new string('a', 255) });

        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorCode("MaximumLengthValidator");
    }

    [Fact]
    public void Email_At254Characters_IsAccepted()
    {
        var local = new string('a', 64);
        var domain = new string('b', 185) + ".com";
        var email = $"{local}@{domain}";

        Assert.Equal(254, email.Length);

        var result = _validator.TestValidate(Valid() with { Email = email });

        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Password_Blank_IsRejected(string password)
    {
        var result = _validator.TestValidate(Valid() with { Password = password });

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_ShorterThan8_IsRejected()
    {
        var result = _validator.TestValidate(Valid() with { Password = "Aa1!aaa" });

        Assert.Equal(7, "Aa1!aaa".Length);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_At8Characters_IsAccepted()
    {
        var result = _validator.TestValidate(Valid() with { Password = "Aa1!aaaa" });

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_LongerThan72Bytes_IsRejected()
    {
        var tooLong = new string('a', 69) + "A1!" + "b";

        Assert.Equal(73, tooLong.Length);

        var result = _validator.TestValidate(Valid() with { Password = tooLong });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at most 72 bytes in UTF-8; accented letters and emoji use more than one byte each.");
    }

    [Fact]
    public void Password_At72Bytes_IsAccepted()
    {
        var password = new string('a', 69) + "A1!";

        Assert.Equal(72, password.Length);

        var result = _validator.TestValidate(Valid() with { Password = password });

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Password_UnderThe72CharactersButOver72Bytes_IsRejected()
    {
        // 40 two-byte characters plus "A1!" is 43 characters but 83 bytes.
        var password = new string('é', 40) + "A1!";

        var result = _validator.TestValidate(Valid() with { Password = password });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at most 72 bytes in UTF-8; accented letters and emoji use more than one byte each.");
    }

    [Fact]
    public void Password_WithoutUppercaseDigitOrSpecialCharacter_ListsEachMissingRule()
    {
        var result = _validator.TestValidate(Valid() with { Password = "lowercase" });

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter.");
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit.");
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one special character.");
    }
}
