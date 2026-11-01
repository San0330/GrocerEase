using GrocerEase.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
        modelBuilder.Entity<User>()
            .Property(u => u.CreatedAt)
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAdd();

        var emailConverter = new ValueConverter<string, string>(
            v => v.ToLowerInvariant(),
            v => v);

        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasConversion(emailConverter);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .HasDatabaseName(UserEmailIndex)
            .IsUnique();
    }
}
