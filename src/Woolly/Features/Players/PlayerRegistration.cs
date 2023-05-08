using MediatR;

using Microsoft.EntityFrameworkCore;

using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Rest.Core;
using Remora.Results;

using Woolly.Data;
using Woolly.Features.Rcon;
using Woolly.Infrastructure;

namespace Woolly.Features.Players;

public sealed class PlayerRegistration : IResponder<IGuildMemberRemove>, IResponder<IGuildMemberUpdate>
{
    private readonly WoollyContext _db;
    private readonly IMediator _mediator;

    public async Task<Result> RespondAsync(IGuildMemberRemove gatewayEvent, CancellationToken ct)
    {
    }

    public async Task<Result> RespondAsync(IGuildMemberUpdate gatewayEvent, CancellationToken ct)
    {
    }
}

public sealed record RemovePlayerCommand(Snowflake GuildId, Snowflake UserId) : IRequest<Result>;

public sealed class RemovePlayerCommandHandler : IRequestHandler<RemovePlayerCommand, Result>
{
    private readonly WoollyContext _db;
    private readonly MojangApi _mojangApi;
    private readonly RconClientFactory _rconClientFactory;

    public async Task<Result> Handle(RemovePlayerCommand request, CancellationToken ct)
    {
        var profiles = await _db.MinecraftPlayers
            .Where(mp => mp.GuildId == request.GuildId && mp.DiscordUserId == request.UserId)
            .Select(mp => new
            {
                mp.MinecraftUsername, mp.MinecraftUuid, ServerNames = mp.Servers.Select(ms => ms.Name).ToList(),
            })
            .ToListAsync(ct);

        foreach (var profile in profiles)
        {
            var username = profile.MinecraftUsername;
            var uuid = profile.MinecraftUuid;
            var updatedFromUuid = false;
            foreach (var server in profile.ServerNames)
            {
            }
        }

        // TODO log
        // TODO return

        async Task<(string, bool)> ProcessServer(string username, string uuid, bool updatedFromUuid, string server)
        {
            var getClient = await _rconClientFactory.GetClientAsync(request.GuildId, server, ct);
            if (!getClient.IsDefined(out var client))
            {
                // TODO warn
                return (username, updatedFromUuid);
            }

            var result = await RemoveFromWhiteListAsync(client, username);
            if (!result.IsDefined(out var removedFromWhiteList))
            {
                // TODO warn
                return (username, updatedFromUuid);
            }

            if (!removedFromWhiteList && !updatedFromUuid)
            {
                // the failure may have been due to the username changing, so refresh it from the UUID
                var getUpdatedProfile = await _mojangApi.GetProfileFromUuidAsync(uuid, ct);
                if (!getUpdatedProfile.IsDefined(out var updatedProfile))
                {
                    // TODO warn
                    return (username, updatedFromUuid);
                }

                updatedFromUuid = true;
                if (username.Equals(updatedProfile.Username, StringComparison.OrdinalIgnoreCase))
                {
                    // TODO warn
                    return (username, updatedFromUuid);
                }

                username = updatedProfile.Username;
                result = await RemoveFromWhiteListAsync(client, username);
            }

            return (username, updatedFromUuid);
        }

        async Task<Result<bool>> RemoveFromWhiteListAsync(RconClient client, string username)
        {
            // TODO also kick
            var deOpResult = await client.DeOpAsync(username, ct);
            if (!deOpResult.IsSuccess) return deOpResult;

            return await client.RemoveFromWhitelistAsync(username, ct);
        }
    }
}
