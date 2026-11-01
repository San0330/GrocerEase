using GrocerEase.Domain.ValueObjects;

namespace GrocerEase.Domain.Entities;

public class User
{
    // For EF Core to materialize a row. Every new user goes through Register.
    private User()
    {
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;

    /// <summary>A BCrypt hash. The password itself is never stored.</summary>
    public string PasswordHash { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }

    public static User Register(
        string firstName,
        string lastName,
        Email email,
        string passwordHash,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        // Npgsql writes timestamptz only from a UTC value, so a local or unspecified time is a bug to catch here.
        if (createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The creation time must be UTC.", nameof(createdAtUtc));

        return new User
        {
            Id = Guid.CreateVersion7(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PasswordHash = passwordHash,
            CreatedAt = createdAtUtc,
        };
    }
}
