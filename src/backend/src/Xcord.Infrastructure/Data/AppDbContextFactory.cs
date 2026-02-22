using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Xcord.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating AppDbContext instances (for EF Core migrations).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        // Use a connection string for design-time only
        // This will be overridden at runtime by the actual configuration
        optionsBuilder.UseNpgsql("Host=localhost;Database=xcord_design;Username=postgres;Password=postgres");

        return new AppDbContext(optionsBuilder.Options);
    }
}
