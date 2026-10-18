using Microsoft.EntityFrameworkCore;

namespace GrocerEase.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSet properties will be added here as domain entities are created
    // e.g. public DbSet<User> Users { get; set; } = null!;
}
