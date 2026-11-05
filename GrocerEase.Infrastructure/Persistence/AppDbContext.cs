using GrocerEase.Domain.Entities;
using GrocerEase.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GrocerEase.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public const string UserEmailIndex = "IX_Users_Email";
    public const string RefreshTokenHashIndex = "IX_RefreshTokens_TokenHash";

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext compares case-insensitively, so the unique index rejects "Ada@x.com" next to "ada@x.com" even for a
        // row written by something that never went through Email.Create. The extension is created by the migration.
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.FirstName).HasMaxLength(User.NameMaxLength);
            user.Property(u => u.LastName).HasMaxLength(User.NameMaxLength);
            user.Property(u => u.PasswordHash).HasMaxLength(User.PasswordHashMaxLength);

            // citext has no length of its own, so the limit on an address is a check constraint.
            user.ToTable(table => table.HasCheckConstraint(
                "CK_Users_Email_MaxLength",
                $"char_length(\"Email\") <= {Email.MaxLength}"));
        });

        // The application supplies the time (IDateTimeProvider), so there is one clock and it can be faked in tests.
        modelBuilder.Entity<User>()
            .Property(u => u.CreatedAt)
            .ValueGeneratedNever();

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasColumnType("citext")
            .HasConversion(email => email.Value, stored => Email.Create(stored));

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .HasDatabaseName(UserEmailIndex)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>(token =>
        {
            token.Property(t => t.TokenHash).HasMaxLength(RefreshToken.TokenHashMaxLength);

            // The application supplies the time, as for users.
            token.Property(t => t.CreatedAt).ValueGeneratedNever();
            token.Property(t => t.ExpiresAt).ValueGeneratedNever();
            token.Property(t => t.RevokedAt).ValueGeneratedNever();

            // A presented token is looked up by its hash, and no two rows may share one.
            token.HasIndex(t => t.TokenHash)
                .HasDatabaseName(RefreshTokenHashIndex)
                .IsUnique();

            // Revoking a family reads every row that shares the id.
            token.HasIndex(t => t.FamilyId);

            token.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Postgres bumps xmin on every update, so two requests that rotate the same token cannot both succeed.
            token.Property<uint>("xmin").IsRowVersion();
        });
    }
}
