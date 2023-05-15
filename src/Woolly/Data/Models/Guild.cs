using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Remora.Rest.Core;

namespace Woolly.Data.Models;

public sealed class Guild
{
    public required Snowflake Id { get; init; }
    public uint Version { get; set; }
    public required string Name { get; set; }
    public Snowflake? MinecraftRoleId { get; set; }
    public Snowflake? JoinChannelId { get; set; }
    public Snowflake? JoinMessageId { get; set; }
    public Snowflake? AdminChannelId { get; set; }

    public List<MinecraftPlayer> MinecraftPlayers { get; } = new();
    public List<MinecraftServer> MinecraftServers { get; } = new();

    public sealed class Configuration : IEntityTypeConfiguration<Guild>
    {
        public void Configure(EntityTypeBuilder<Guild> builder)
        {
            builder
                .Property(g => g.Version)
                .IsRowVersion();
        }
    }
}
