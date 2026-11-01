namespace GrocerEase.Domain.Exceptions;

/// <summary>What kind of failure an <see cref="IAppException"/> is, without saying how a caller is told about it.</summary>
public enum ErrorKind
{
    /// <summary>The request was understood but a value or the state it needs is not acceptable.</summary>
    Invalid,

    /// <summary>The caller could not be identified, or proved who they are incorrectly.</summary>
    Unauthenticated,

    /// <summary>The request clashes with something that already exists.</summary>
    Conflict,
}
