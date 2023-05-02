using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Remora.Rest.Core;

namespace Woolly.Data.Models;

[EntityTypeConfiguration(typeof(Configuration))]
public sealed class MinecraftServer
{
    public int Id { get; init; }
    public required Snowflake GuildId { get; init; }
    public required string Name { get; set; }
    public required string Host { get; set; }
    public required ushort RconPort { get; set; }
    public required string RconPassword { get; set; }
    public required ushort PingPort { get; set; }

    public sealed class Configuration : IEntityTypeConfiguration<MinecraftServer>
    {
        public void Configure(EntityTypeBuilder<MinecraftServer> builder)
        {
            builder
                .HasIndex(ms => new { ms.GuildId, ms.Name })
                .IsUnique();
        }
    }
}
