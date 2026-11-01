namespace GrocerEase.Domain.Exceptions;

/// <summary>
/// A failure the caller can understand and act on, as opposed to a bug. The host decides how each
/// <see cref="ErrorKind"/> is presented, so nothing here mentions HTTP.
/// </summary>
public interface IAppException
{
    ErrorKind Kind { get; }
    string ErrorMessage { get; }
    string ErrorCode { get; }

    /// <summary>A short summary shown as the response title. When <c>null</c> the host picks a generic one.</summary>
    string? Title => null;
}
