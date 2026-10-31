namespace GrocerEase.Domain.Exceptions;

public interface IAppException
{
    int StatusCode { get; }
    string ErrorMessage { get; }
    string ErrorCode { get; }

    /// <summary>Overrides the HTTP reason phrase used as the response title.</summary>
    string? Title => null;
}
