using Hangfire;

using MediatR;

using Microsoft.EntityFrameworkCore;

using Polly;

using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Rest;
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
    private readonly IBackgroundJobClientV2 _jobClient;
    private readonly WoollyContext _db;
    private readonly MojangApi _mojangApi;
    private readonly RconClientFactory _rconClientFactory;

    public async Task<Result> Handle(RemovePlayerCommand request, CancellationToken ct)
    {
        var profiles = await _db.MinecraftPlayers
            .Where(mp => mp.GuildId == request.GuildId && mp.DiscordUserId == request.UserId)
            .ToListAsync(ct);

        // ******************************************************************
        // TODO members are now restricted to one Minecraft profile per guild
        // ******************************************************************
        foreach (var profile in profiles)
        {
            var username = profile.MinecraftUsername;
            var uuid = profile.MinecraftUuid;
            var updatedFromUuid = false;
            var removeProfile = true;

            foreach (var server in profile.Servers)
            {
                // there's two failure modes here:
                // 1) IsSuccess is false, this means there was a communication error to the client (server was offline,
                // restarted during the operation, etc.)
                // 2) Entity is false, this means the whitelist operation couldn't find the user to remove, this in turn
                // could be from one of two things:
                // 1) the user is not actually on the whitelist (they were never on it, or someone else already removed
                // them)
                // 2) the user's username has changed since they were initially whitelisted. The whitelist commands
                // rather unhelpfully do *not* take UUIDs, even though the whitelist system uses them internally. The
                // workaround is to look up the user's current name via the Mojang API, and then try again.
                // 3) the user's account has been deleted, in which case the lookup would return NotFound (I think?)

                var result = await Policy
                    .HandleResult<Result<bool>>(r => !r.IsSuccess || r.Entity is false)
                    .RetryAsync(3, async (_, _) =>
                    {
                        if (updatedFromUuid) return;
                        var getProfile = await _mojangApi.GetProfileFromUuidAsync(uuid, ct);
                        switch (getProfile)
                        {
                            case { Error: NotFoundError }:
                                updatedFromUuid = true;
                                break;
                            case { Entity.Username: { } updatedUsername }:
                                username = updatedUsername;
                                updatedFromUuid = true;
                                break;
                        }
                    })
                    .ExecuteAsync(async () =>
                    {
                        var getClient = await _rconClientFactory.GetClientAsync(request.GuildId, server.Name, ct);
                        if (!getClient.IsDefined(out var client)) return Result<bool>.FromError(getClient);

                        var deop = await client.DeOpAsync(username, ct);
                        if (!deop.IsSuccess) return deop;

                        var dewhitelist = await client.RemoveFromWhitelistAsync(username, ct);
                        if (!dewhitelist.IsSuccess) return dewhitelist;

                        var kick = await client.KickAsync(username, "You left the Discord server", ct);
                        if (!kick.IsSuccess) return kick;

                        return dewhitelist;
                    });

                switch ((result.IsSuccess, result.Entity, updatedFromUuid))
                {
                    case (false, _, _):
                    case (true, false, false):
                        // completely failed
                        // - or -
                        // user could not be found on whitelist, and the username could not be updated from the UUID
                        // need to retry later
                        break;
                    default:
                        // user was removed from whitelist, or not on it to begin with
                        break;
                }
            }

            if (removeProfile) _db.MinecraftPlayers.Remove(profile);
        }

        // TODO log/warn
        // TODO in event of failure, maybe schedule a background job to try again in 15 minutes or something?
        // TODO if the username was updated, update the DB accordingly
        // TODO return an aggregated result
    }
}
