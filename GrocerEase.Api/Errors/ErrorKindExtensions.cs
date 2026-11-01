using GrocerEase.Domain.Exceptions;

namespace GrocerEase.Api.Errors;

public static class ErrorKindExtensions
{
    /// <summary>The one place a domain failure becomes an HTTP status.</summary>
    public static int ToStatusCode(this ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No HTTP status is defined for this kind."),
    };
}
