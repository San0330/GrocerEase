namespace GrocerEase.Domain.Exceptions;

/// <summary>Codes for failures that mean something in the domain. Clients branch on these strings, so never change one.</summary>
public static class ErrorCodes
{
    public const string InvalidCredentials = "User.InvalidCredentials";
    public const string DuplicateEmail = "User.DuplicateEmail";
}
