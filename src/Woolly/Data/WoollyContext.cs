using EntityFramework.Exceptions.PostgreSQL;

using Microsoft.EntityFrameworkCore;

using Remora.Rest.Core;

using Woolly.Data.Models;

namespace Woolly.Data;

public sealed class WoollyContext : DbContext
{
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<MinecraftServer> MinecraftServers => Set<MinecraftServer>();

    public WoollyContext(DbContextOptions<WoollyContext> options) : base(options) { }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<Snowflake>()
            .HaveConversion<DiscordSnowflakeConverter>();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseExceptionProcessor()
            .UseSnakeCaseNamingConvention();
    }
}
