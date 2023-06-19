using EntityFramework.Exceptions.PostgreSQL;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Remora.Rest.Core;

using Woolly.Data.Models;

namespace Woolly.Data;

public sealed class WoollyContext : DbContext
{
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<PlayerServerMembership> PlayerServerMemberships => Set<PlayerServerMembership>();
    public DbSet<MinecraftPlayer> MinecraftPlayers => Set<MinecraftPlayer>();
    public DbSet<MinecraftServer> MinecraftServers => Set<MinecraftServer>();
    public DbSet<State> States => Set<State>();

    static WoollyContext()
    {
#pragma warning disable CS0618
        // this is deprecated, but the new API is really awkward to use, and should be nicer come .NET 8
        NpgsqlConnection.GlobalTypeMapper
            .MapEnum<MembershipState>();
#pragma warning restore CS0618
    }

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
            .UseNpgsql(o => o.UseNodaTime())
            .UseExceptionProcessor()
            .UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum<MembershipState>()
            .ApplyConfigurationsFromAssembly(typeof(WoollyContext).Assembly);
    }
}
