using System.Text.Json;

using EntityFramework.Exceptions.Common;

using MediatR;

using Microsoft.EntityFrameworkCore;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Builders;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

using Woolly.Data;
using Woolly.Data.Models;
using Woolly.Extensions;
using Woolly.Features.Guilds;
using Woolly.Infrastructure;

namespace Woolly.Features.Players;

[Group("whitelist")]
[RequireContext(ChannelContext.Guild)]
public sealed class WhitelistCommands : CommandGroup
{
    private readonly FeedbackService _feedback;
    private readonly IDiscordRestInteractionAPI _interactionApi;
    private readonly IInteractionContext _interactionContext;
    private readonly IMediator _mediator;

    [Command("add")]
    public async Task<IResult> AddToWhitelist(
        [AutocompleteProvider(MinecraftServerNameAutocompleter.Identity)]
        string server)
    {
        var joinResult = await _mediator.Send(new JoinServerCommand(
            _interactionContext.GetGuildId(),
            _interactionContext.GetUserId(),
            server));
        if (joinResult.IsSuccess)
        {
            return await _feedback.SendContextualSuccessAsync(
                $"You have been whitelisted on {server}.",
                options: new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral),
                ct: CancellationToken);
        }

        switch (joinResult.Error)
        {
            case NotFoundError:
                return await _feedback.SendContextualErrorAsync(
                    $"Couldn't find a configured server named {server}. Please try again.",
                    options: new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral),
                    ct: CancellationToken);
            case UsernameNotAvailableError:
                var modal = AddPlayerInteractions.CreateUsernameModal(
                    "We don't know your Minecraft username, tell us so we can whitelist you.");
                return await _interactionApi.CreateInteractionResponseAsync(
                    _interactionContext.Interaction.ID,
                    _interactionContext.Interaction.Token,
                    modal,
                    ct: CancellationToken);
        }

        return joinResult;
    }
}

public sealed record JoinServerCommand(Snowflake GuildId, Snowflake DiscordUserId, string ServerName) : IRequest<Result>;

public sealed class JoinServerCommandHandler : IRequestHandler<JoinServerCommand, Result>
{
    public async Task<Result> Handle(JoinServerCommand request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}

[Group(GroupName)]
public sealed class AddPlayerInteractions : InteractionGroup
{
    public const string GroupName = "add-player";
    public const string UsernameModal = "minecraft-username";

    private readonly FeedbackService _feedback;
    private readonly IDiscordRestInteractionAPI _interactionApi;
    private readonly IInteractionContext _interactionContext;
    private readonly IMediator _mediator;
    private readonly StateService _state;

    public AddPlayerInteractions(FeedbackService feedback, IDiscordRestInteractionAPI interactionApi,
        IInteractionContext interactionContext, IMediator mediator, StateService state)
    {
        _feedback = feedback;
        _interactionApi = interactionApi;
        _interactionContext = interactionContext;
        _mediator = mediator;
        _state = state;
    }

    [Modal(UsernameModal)]
    public async Task<IResult> OnUsernameSubmitted(string username, string state)
    {
        var getData = await _state.TryTakeAsync(int.Parse(state), CancellationToken);
        if (!getData.IsDefined(out var data)) return getData;
        var serverNames = JsonSerializer.Deserialize<List<string>>(data)!;

        var setUsername = await _mediator.Send(new SetPlayerMinecraftUsernameCommand(
            _interactionContext.GetGuildId(),
            _interactionContext.GetUserId(),
            username), CancellationToken);

        if (setUsername.Error is NotFoundError)
        {
            return await SendUsernameModalAsync(
                serverNames,
                inputLabel: "We couldn't find that username. Double check your typing and try again:");
        }

        if (!setUsername.IsSuccess) return setUsername;

        var updateServers = await _mediator.Send(new UpdatePlayerServersCommand(
            _interactionContext.GetGuildId(),
            _interactionContext.GetUserId(),
            serverNames), CancellationToken);

        if (!updateServers.IsSuccess) return updateServers;

        return await _feedback.SendContextualSuccessAsync(
            $"You have been added to {string.Join(", ", serverNames)}.",
            options: new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral),
            ct: CancellationToken);
    }

    public static ModalBuilder CreateUsernameModal(string inputLabel)
    {
        return new ModalBuilder()
            .WithTitle("Minecraft username")
            .WithCustomID(CustomIDHelpers.CreateModalID(UsernameModal, GroupName))
            .AddForm(new TextInputBuilder()
                .WithStyle(TextInputStyle.Short)
                .WithCustomID("username")
                .WithLabel(inputLabel)
                .IsRequired()
                .WithPlaceholder("username"));
    }
}

public sealed record UpdatePlayerServersCommand(
    Snowflake GuildId,
    Snowflake UserId,
    IReadOnlyList<string> ServerNames) : IRequest<Result>;

public sealed record SetPlayerMinecraftUsernameCommand(
    Snowflake GuildId,
    Snowflake DiscordUserId,
    string MinecraftUsername) : IRequest<Result>;

public sealed class SetPlayerMinecraftUsernameCommandHandler : IRequestHandler<SetPlayerMinecraftUsernameCommand, Result>
{
    private readonly WoollyContext _db;
    private readonly ILogger<SetPlayerMinecraftUsernameCommandHandler> _logger;
    private readonly MojangApi _mojangApi;

    public async Task<Result> Handle(SetPlayerMinecraftUsernameCommand request, CancellationToken ct)
    {
        var getProfile = await _mojangApi.GetProfileFromUsernameAsync(request.MinecraftUsername, ct);
        if (!getProfile.IsDefined(out var profile)) return (Result)getProfile;

        var player = await _db.MinecraftPlayers
            .FirstOrDefaultAsync(mp => mp.GuildId == request.GuildId && mp.DiscordUserId == request.DiscordUserId, ct);

        if (player is null)
        {
            player = new MinecraftPlayer
            {
                GuildId = request.GuildId,
                DiscordUserId = request.DiscordUserId,
                MinecraftUsername = profile.Username,
                MinecraftUuid = profile.Uuid,
            };
            _db.MinecraftPlayers.Add(player);
        }
        else
        {
            player.MinecraftUsername = profile.Username;
            player.MinecraftUuid = profile.Uuid;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException uce)
        {
            // TODO allow admins to override this
            // TODO say who it's assigned to, the Entries on UniqueConstraintException may have the info
            return new InvalidOperationError("That Minecraft account is already assigned to someone else.");
        }

        return Result.FromSuccess();
    }
}

public sealed class MovePlayerJoinMessage : INotificationHandler<GuildConfigurationUpdated>
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly WoollyContext _db;

    public async Task Handle(GuildConfigurationUpdated notification, CancellationToken ct)
    {
        var (oldConfig, newConfig) = notification;

        if (oldConfig.JoinChannelId == newConfig.JoinChannelId) return;
        if (oldConfig is { JoinChannelId: { } channelId, JoinMessageId: { } messageId })
        {
            var delete = await _channelApi.DeleteMessageAsync(
                channelId,
                messageId,
                "Moving Woolly join message",
                ct);
            if (!delete.IsSuccess)
            {
                // TODO report error to admin channel
                // if it's "not found" (if that can be determined), just ignore it, as an admin may have already deleted it
            }
        }

        var serverNames = await _db.MinecraftServers
            .Where(ms => ms.GuildId == newConfig.GuildId)
            .Select(ms => ms.Name)
            .ToListAsync(ct);

        var messageBuilder = AddPlayerInteractions.BuildJoinMessage();

        var send = await _channelApi.CreateMessageAsync(newConfig.JoinChannelId!.Value, messageBuilder, ct: ct);
        if (!send.IsDefined(out var message))
        {
            // TODO report? throw?
        }

        var guild = await _db.Guilds.FirstAsync(g => g.Id == newConfig.GuildId, ct);
        guild.JoinMessageId = message.ID;
        await _db.SaveChangesAsync(ct);
    }
}