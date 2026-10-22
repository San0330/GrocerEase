namespace GrocerEase.Domain.Exceptions;

public interface IAppException
{
    int StatusCode { get; }
    string ErrorMessage { get; }
}
