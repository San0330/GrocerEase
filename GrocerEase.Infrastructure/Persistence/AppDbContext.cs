using GrocerEase.Domain.Entities;
using GrocerEase.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace GrocerEase.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public const string UserEmailIndex = "IX_Users_Email";

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The application supplies the time (IDateTimeProvider), so there is one clock and it can be faked in tests.
        modelBuilder.Entity<User>()
            .Property(u => u.CreatedAt)
            .ValueGeneratedNever();

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasConversion(email => email.Value, stored => Email.Create(stored));

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .HasDatabaseName(UserEmailIndex)
            .IsUnique();
    }
}
