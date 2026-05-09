using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace Synapse.Infrastructure.Data;

/// <summary>
/// Used by `dotnet ef migrations add` at design time.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SynapseDbContext>
{
    public SynapseDbContext CreateDbContext(string[] args)
    {
        var connectionString = "Host=localhost;Port=5432;Database=synapse;Username=postgres;Password=postgres;";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<SynapseDbContext>();
        optionsBuilder.UseNpgsql(dataSource, npg =>
        {
            npg.UseNetTopologySuite();
            npg.UseVector();
        });

        return new SynapseDbContext(optionsBuilder.Options);
    }
}
