using MediatR;

using Remora.Commands.Attributes;
using Remora.Discord.Commands.Attributes;
using Remora.Rest.Core;
using Remora.Results;

using Woolly.Infrastructure;

namespace Woolly.Features.ServerListPing;

public sealed partial class ServerListPingCommands
{
    [Command("ping")]
    public async Task<IResult> Ping([AutocompleteProvider(MinecraftServerNameAutocompleter.Identity)] string server)
    {
        var result =
            await _mediator.Send(new PingQuery(_requestContext.InteractionContext!.Value.GuildId!.Value, server));
        if (!result.IsSuccess) return result;
        return await _feedback.SendContextualSuccessAsync(
            $"`{server}` is responding. Ping took {result.Entity.TotalMilliseconds}ms.");
    }
}

public sealed record PingQuery(Snowflake GuildId, string ServerName) : IRequest<Result<TimeSpan>>;

public sealed class PingQueryHandler : IRequestHandler<PingQuery, Result<TimeSpan>>
{
    private readonly ServerListPingClientFactory _pingClientFactory;

    public PingQueryHandler(ServerListPingClientFactory pingClientFactory) => _pingClientFactory = pingClientFactory;

    public async Task<Result<TimeSpan>> Handle(PingQuery request, CancellationToken cancellationToken)
    {
        var getClient = await _pingClientFactory.GetClientAsync(request.GuildId, request.ServerName, cancellationToken);
        if (!getClient.IsSuccess) return Result<TimeSpan>.FromError(getClient);
        var client = getClient.Entity;

        return await client.PingAsync(cancellationToken);
    }
}
