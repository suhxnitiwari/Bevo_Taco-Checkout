using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BevosTacos.Data;

//Used only by `dotnet ef migrations add`. Migrations target Postgres (production);
//SQLite dev and test databases are built straight from the model instead.
//No database connection is needed to generate a migration; CI sets BEVOS_POSTGRES to apply them.
public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("BEVOS_POSTGRES") ?? "Host=localhost;Database=bevostacos")
            .Options);
}
