using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Remora.Rest.Core;

namespace Woolly.Data.Models;

public sealed class MinecraftPlayer
{
    public int Id { get; init; }
    public required Snowflake GuildId { get; init; }
    public required Snowflake DiscordUserId { get; init; }
    public required string MinecraftUsername { get; set; }
    public required string MinecraftUuid { get; set; }

    public List<MinecraftServer> Servers { get; } = new();

    public sealed class Configuration : IEntityTypeConfiguration<MinecraftPlayer>
    {
        public void Configure(EntityTypeBuilder<MinecraftPlayer> builder)
        {
            builder
                .HasIndex(mp => new { mp.GuildId, mp.MinecraftUuid })
                .IsUnique();
        }
    }
}
