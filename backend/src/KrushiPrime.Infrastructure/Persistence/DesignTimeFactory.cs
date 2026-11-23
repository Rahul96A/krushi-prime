using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KrushiPrime.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef migrations` – migrations target Azure SQL / SQL Server.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=krushiprime;Trusted_Connection=True;")
            .Options;
        return new AppDbContext(options, new SystemUser());
    }
}
