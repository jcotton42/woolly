using MediatR;

using Microsoft.EntityFrameworkCore;

using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.Gateway.Responders;
using Remora.Rest.Core;
using Remora.Results;

using Woolly.Data;
using Woolly.Data.Models;

namespace Woolly.Features;

public sealed class GuildEvents : IResponder<IGuildCreate>, IResponder<IGuildDelete>, IResponder<IGuildUpdate>
{
    private readonly IMediator _mediator;

    public GuildEvents(IMediator mediator) => _mediator = mediator;
    // TODO handle leave/kick/ban to remove players from the whitelist
    public async Task<Result> RespondAsync(IGuildCreate gatewayEvent, CancellationToken ct)
    {
        if (gatewayEvent.Guild.Value is IGuildCreate.IAvailableGuild guild)
        {
            return await _mediator.Send(new UpsertGuildCommand(guild.ID, guild.Name), ct);
        }
        return Result.FromSuccess();
    }

    // TODO unit test this logic, it's destructive
    public async Task<Result> RespondAsync(IGuildDelete gatewayEvent, CancellationToken ct)
    {
        // from the docs: If the unavailable field is **not** set, the user was removed from the guild.
        if (gatewayEvent.IsUnavailable.HasValue) return Result.FromSuccess();
        return await _mediator.Send(new RemoveGuildCommand(gatewayEvent.ID), ct);
    }

    public async Task<Result> RespondAsync(IGuildUpdate gatewayEvent, CancellationToken ct)
        => await _mediator.Send(new UpsertGuildCommand(gatewayEvent.ID, gatewayEvent.Name), ct);
}

public sealed record UpsertGuildCommand(Snowflake Id, string Name) : IRequest<Result>;
public sealed partial class UpsertGuildCommandHandler : IRequestHandler<UpsertGuildCommand, Result>
{
    private readonly WoollyContext _db;
    private readonly ILogger<UpsertGuildCommandHandler> _logger;

    public UpsertGuildCommandHandler(WoollyContext db, ILogger<UpsertGuildCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> Handle(UpsertGuildCommand request, CancellationToken cancellationToken)
    {
        var guild = await _db.Guilds.FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);
        if (guild is null)
        {
            guild = new Guild { Id = request.Id, Name = request.Name };
            _db.Guilds.Add(guild);
        }
        else
        {
            if (guild.Name == request.Name) return Result.FromSuccess();
            guild.Name = request.Name;
        }

        await _db.SaveChangesAsync(cancellationToken);
        GuildUpserted(request.Id, request.Name);
        return Result.FromSuccess();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Upserted guild with ID {GuildId} and name `{GuildName}`")]
    private partial void GuildUpserted(Snowflake guildId, string guildName);
}

public sealed record RemoveGuildCommand(Snowflake Id) : IRequest<Result>;
public sealed partial class RemoveGuildCommandHandler : IRequestHandler<RemoveGuildCommand, Result>
{
    private readonly WoollyContext _db;
    private readonly ILogger<RemoveGuildCommandHandler> _logger;

    public RemoveGuildCommandHandler(WoollyContext db, ILogger<RemoveGuildCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> Handle(RemoveGuildCommand request, CancellationToken cancellationToken)
    {
        var count = await _db.Guilds.Where(g => g.Id == request.Id).ExecuteDeleteAsync(cancellationToken);
        if (count > 0) GuildRemoved(request.Id);
        return Result.FromSuccess();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Removed guild with ID {GuildId}")]
    private partial void GuildRemoved(Snowflake guildId);
}
