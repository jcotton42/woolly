using Immediate.Handlers.Shared;

using Remora.Discord.API.Abstractions.Gateway.Events;

using Remora.Discord.Gateway.Responders;
using Remora.Rest.Core;
using Remora.Results;

[Handler]
public static partial class RegisterGuildCommand
{
    public record struct Command(IGuildCreate Guild);

    private static ValueTask<Result> HandleAsync(Command c, ILogger<Handler> logger, CancellationToken token)
    {
        if (c.Guild.Guild.Value is not IGuildCreate.IAvailableGuild guild)
        {
            return ValueTask.FromResult(Result.FromSuccess());
        }

        logger.GuildJoined(guild.Name, guild.ID);

        return ValueTask.FromResult(Result.FromSuccess());
    }

    [LoggerMessage(
        EventId = 0,
        EventName = "Guild joined",
        Level = LogLevel.Information,
        Message = "Guild `{Name}` ({ID}) joined")]
    private static partial void GuildJoined(this ILogger logger, string name, Snowflake id);
}

public static partial class RegisterGuildCommand
{
    public sealed partial class Handler : IResponder<IGuildCreate>
    {
        public async Task<Result> RespondAsync(IGuildCreate gatewayEvent, CancellationToken ct)
        {
            return await HandleAsync(new Command(gatewayEvent), ct);
        }
    }
}
