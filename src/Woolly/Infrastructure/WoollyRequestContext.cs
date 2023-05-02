using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Rest.Core;

namespace Woolly.Infrastructure;

public sealed class WoollyRequestContext
{
    public string RequestId { get; } = Guid.NewGuid().ToString("D");
    public WoollyInteractionContext? InteractionContext { get; }

    public WoollyRequestContext(ContextInjectionService contextInjection)
    {
        if (contextInjection.Context?.TryGetUserID(out var userId) is not true) return;

        var guildId = contextInjection.Context.TryGetGuildID(out var id) ? id : null;
        var channelId = contextInjection.Context.TryGetChannelID(out id) ? id : null;
        InteractionContext =
            new WoollyInteractionContext { UserId = userId.Value, GuildId = guildId, ChannelId = channelId };
    }
}

public readonly struct WoollyInteractionContext
{
    public required Snowflake UserId { get; init; }
    public required Snowflake? GuildId { get; init; }
    public required Snowflake? ChannelId { get; init; }
}
