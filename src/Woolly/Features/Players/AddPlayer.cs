using System.Text.Json;

using MediatR;

using Microsoft.EntityFrameworkCore;

using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Builders;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

using Woolly.Data;
using Woolly.Extensions;
using Woolly.Features.Guilds;
using Woolly.Infrastructure;

namespace Woolly.Features.Players;

[Group("add-player")]
public sealed class AddPlayerInteractions : InteractionGroup
{
    private readonly FeedbackService _feedback;
    private readonly IDiscordRestInteractionAPI _interactionApi;
    private readonly IInteractionContext _interactionContext;
    private readonly IMediator _mediator;
    private readonly StateService _state;

    [SelectMenu("servers")]
    public async Task<IResult> OnServersSelected(IReadOnlyList<string> values)
    {
        var update = await _mediator.Send(new UpdatePlayerServersCommand(
            _interactionContext.GetGuildId(),
            _interactionContext.GetUserId(),
            values), CancellationToken);

        if (update.IsSuccess)
        {
            return await _feedback.SendContextualSuccessAsync(
                $"You have been added to {string.Join(", ", values)}.",
                options: new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral),
                ct: CancellationToken);
        }

        if (update.Error is not UsernameNotAvailableError) return update;

        var state = await _state.AddAsync(JsonSerializer.Serialize(values), CancellationToken);
        var modal = new ModalBuilder(Title: "Minecraft username")
            .WithCustomID(CustomIDHelpers.CreateModalIDWithState("minecraft-username", state.ToString(), "add-player"))
            .AddForm(new TextInputBuilder(Style: TextInputStyle.Short)
                .WithCustomID("username")
                .WithLabel("We don't know your Minecraft username. Please tell us so you can be whitelisted.")
                .IsRequired()
                .WithPlaceholder("username"));

        return await _interactionApi.CreateInteractionResponseAsync(
            _interactionContext.Interaction.ID,
            _interactionContext.Interaction.Token,
            modal);
    }

    [Modal("minecraft-username")]
    public async Task<IResult> OnUsernameSubmitted(string username, string state)
    {
        var data = await _state.TakeAsync(int.Parse(state), CancellationToken);
        var serverNames = JsonSerializer.Deserialize<List<string>>(data)!;

        var setUsername = await _mediator.Send(new SetPlayerMinecraftUsernameCommand(
            _interactionContext.GetGuildId(),
            _interactionContext.GetUserId(),
            username), CancellationToken);

        if (!setUsername.IsSuccess)
        {
            // TODO handle things like the username not existing (maybe have the command return NotFoundError?)
        }

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
}

public sealed record UsernameNotAvailableError(
    string Message = "No Minecraft username is available for this user.") : ResultError(Message);

public sealed record UpdatePlayerServersCommand(
    Snowflake GuildId,
    Snowflake UserId,
    IReadOnlyList<string> ServerNames) : IRequest<Result>;

public sealed record SetPlayerMinecraftUsernameCommand(
    Snowflake GuildId,
    Snowflake DiscordUserId,
    string MinecraftUsername) : IRequest<Result>;

public sealed class MovePlayerJoinMessage : INotificationHandler<GuildConfigurationUpdated>
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly WoollyContext _db;

    // TODO also handle updating this when the server list is updated
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
            }
        }

        var serverNames = await _db.MinecraftServers
            .Where(ms => ms.GuildId == newConfig.GuildId)
            .Select(ms => ms.Name)
            .ToListAsync(ct);

        var messageBuilder = new MessageBuilder()
            .WithContent("Select the servers you want to join from the list below")
            .AddComponent(new SelectBuilder()
                .WithCustomID(CustomIDHelpers.CreateSelectMenuID("servers", "add-player"))
                .AddOptions(serverNames.Select(name => new SelectOption(name, name)))
                .WithMaxValues(int.MaxValue)
                .WithPlaceholder("Minecraft servers")
                .Build()
            );

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
