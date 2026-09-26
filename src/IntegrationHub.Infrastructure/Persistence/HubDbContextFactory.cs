using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace IntegrationHub.Infrastructure.Persistence;

public sealed class HubDbContextFactory : IDesignTimeDbContextFactory<HubDbContext>
{
    public HubDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__IntegrationHub") ?? "";
        return new HubDbContext(new DbContextOptionsBuilder<HubDbContext>().UseSqlServer(connection).Options);
    }
}
