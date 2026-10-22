namespace GrocerEase.Domain.Exceptions;

public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Email or password is invalid.")
    {
    }
}
