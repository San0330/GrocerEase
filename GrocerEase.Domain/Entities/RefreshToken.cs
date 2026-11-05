namespace GrocerEase.Domain.Entities;

/// <summary>
/// One refresh token. Tokens that descend from the same login share a <see cref="FamilyId"/>: each use replaces the
/// token with a successor and revokes it, so a revoked token that shows up again was copied, and the whole family
/// can be revoked.
/// </summary>
public class RefreshToken
{
    /// <summary>Room for a SHA-256 hash written as base64 (44 characters), with space for a longer digest later.</summary>
    public const int TokenHashMaxLength = 64;

    // For EF Core to materialize a row. Every new token goes through StartFamily or Rotate.
    private RefreshToken()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }

    /// <summary>A hash of the token. The token itself is never stored, so a leaked table cannot be replayed.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAt;

    /// <summary>Starts a new family, for a login or a registration.</summary>
    public static RefreshToken StartFamily(Guid userId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc) =>
        Create(userId, Guid.CreateVersion7(), tokenHash, createdAtUtc, expiresAtUtc);

    /// <summary>Revokes this token and returns its successor in the same family.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTime nowUtc, DateTime expiresAtUtc)
    {
        if (IsRevoked)
            throw new InvalidOperationException("A revoked token cannot be rotated.");
        if (IsExpired(nowUtc))
            throw new InvalidOperationException("An expired token cannot be rotated.");

        var successor = Create(UserId, FamilyId, newTokenHash, nowUtc, expiresAtUtc);
        Revoke(nowUtc);
        return successor;
    }

    /// <summary>Revokes the token. Calling it again keeps the first time.</summary>
    public void Revoke(DateTime nowUtc)
    {
        RequireUtc(nowUtc, nameof(nowUtc));
        RevokedAt ??= nowUtc;
    }

    private static RefreshToken Create(
        Guid userId,
        Guid familyId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        RequireUtc(createdAtUtc, nameof(createdAtUtc));
        RequireUtc(expiresAtUtc, nameof(expiresAtUtc));
        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("The expiry must be after the creation time.", nameof(expiresAtUtc));

        return new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAt = createdAtUtc,
            ExpiresAt = expiresAtUtc,
        };
    }

    // Npgsql writes timestamptz only from a UTC value, so a local or unspecified time is a bug to catch here.
    private static void RequireUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The time must be UTC.", name);
    }
}
