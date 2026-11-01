namespace GrocerEase.Domain.ValueObjects;

/// <summary>
/// An email address in its one stored form: lower case. Addresses that differ only in case are the same mailbox for our
/// purposes, so normalizing here means nothing else has to remember to, and two <see cref="Email"/>s are equal exactly
/// when they would collide as a user's login.
/// </summary>
public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    /// <summary>Wraps an address, lower-casing it. Whether it is well formed is the validator's concern.</summary>
    public static Email Create(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        return new Email(address.ToLowerInvariant());
    }

    public override string ToString() => Value;
}
