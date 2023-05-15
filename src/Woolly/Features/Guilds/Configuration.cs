using System.ComponentModel;

using MediatR;

using Microsoft.EntityFrameworkCore;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

using Woolly.Data;
using Woolly.Data.Models;

namespace Woolly.Features.Guilds;

[RequireContext(ChannelContext.Guild)]
public sealed class ConfigurationCommands : CommandGroup
{
    private readonly IInteractionCommandContext _commandContext;
    private readonly FeedbackService _feedback;
    private readonly IMediator _mediator;

    public ConfigurationCommands(IInteractionCommandContext commandContext, FeedbackService feedback, IMediator mediator)
    {
        _commandContext = commandContext;
        _feedback = feedback;
        _mediator = mediator;
    }

    [Command("configure")]
    [Description("Configures Woolly. Note that all parameters must be filled in the first time you run this.")]
    public async Task<IResult> Configure(
        [Description("The channel to place the join message in.")]
        IChannel? joinChannel = null,
        [Description(
            "A channel for Woolly to send error/warning/etc. messages in. May be a thread, recommendation is that "
            + "only moderators be able to access this channel.")]
        IChannel? adminChannel = null)
    {
        var guildId = _commandContext.Interaction.GuildID.Value;

        var tryUpdate = await _mediator.Send(new UpdateGuildConfigurationCommand(new GuildConfiguration(
            guildId,
            joinChannel?.ID,
            default,
            adminChannel?.ID)));
        if (!tryUpdate.IsSuccess) return tryUpdate;

        return await _feedback.SendContextualSuccessAsync("Configuration updated.", ct: CancellationToken);
    }
}

public sealed record GuildConfiguration(Snowflake GuildId, Snowflake? JoinChannelId, Snowflake? JoinMessageId, Snowflake? AdminChannelId)
{
    public static GuildConfiguration FromGuildEntity(Guild guild) =>
        new GuildConfiguration(guild.Id, guild.JoinChannelId, guild.JoinMessageId, guild.AdminChannelId);
}

public sealed record UpdateGuildConfigurationCommand(GuildConfiguration Patch) : IRequest<Result>;

public sealed record GuildConfigurationUpdated(GuildConfiguration Old, GuildConfiguration New) : INotification;

public sealed class UpdateGuildConfigurationCommandHandler : IRequestHandler<UpdateGuildConfigurationCommand, Result>
{
    private readonly WoollyContext _db;
    private readonly IMediator _mediator;

    public UpdateGuildConfigurationCommandHandler(WoollyContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result> Handle(UpdateGuildConfigurationCommand request, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        var guild = await _db.Guilds.FirstAsync(g => g.Id == request.Patch.GuildId, ct);
        var oldConfig = GuildConfiguration.FromGuildEntity(guild);

        if (request.Patch.AdminChannelId is not null) guild.AdminChannelId = request.Patch.AdminChannelId;
        if (request.Patch.JoinChannelId is not null) guild.JoinChannelId = request.Patch.JoinChannelId;

        if (guild.AdminChannelId is null || guild.JoinChannelId is null)
        {
            return new ArgumentInvalidError(nameof(request), "One or more required configuration settings weren't set.");
        }

        var newConfig = GuildConfiguration.FromGuildEntity(guild);
        await _db.SaveChangesAsync(ct);
        await _mediator.Publish(new GuildConfigurationUpdated(oldConfig, newConfig), ct);
        await transaction.CommitAsync(ct);

        return Result.FromSuccess();
    }
}
