using EntityFramework.Exceptions.PostgreSQL;

using Microsoft.EntityFrameworkCore;

using Remora.Rest.Core;

using Woolly.Data.Models;

namespace Woolly.Data;

public sealed class WoollyContext : DbContext
{
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<MinecraftPlayer> MinecraftPlayers => Set<MinecraftPlayer>();
    public DbSet<MinecraftServer> MinecraftServers => Set<MinecraftServer>();
    public DbSet<State> States => Set<State>();

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WoollyContext).Assembly);
    }
}
