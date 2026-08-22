using Microsoft.EntityFrameworkCore;
using WellSense.Infrastructure.Persistence;

namespace WellSense.Tests.TestHelpers;

public static class InMemoryDbContextFactory
{
    public static WellSenseDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<WellSenseDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new WellSenseDbContext(options);
    }
}
