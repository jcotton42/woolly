using EntityFramework.Exceptions.Common;

using MediatR;

using Remora.Commands.Attributes;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

using Woolly.Data;
using Woolly.Data.Models;

namespace Woolly.Features.Servers;

public sealed partial class ServerCommands
{
    [Command("add")]
    [SuppressInteractionResponse(true)]
    // TODO authorization
    public async Task<IResult> AddServerAsync()
    {
        var response = new InteractionResponse(
            InteractionCallbackType.Modal,
            new(
                new InteractionModalCallbackData(
                    CustomIDHelpers.CreateModalID("add", "server"),
                    "Add a Minecraft Server",
                    new[]
                    {
                        new ActionRowComponent(
                            new[]
                            {
                                new TextInputComponent(
                                    CustomID: "name",
                                    Style: TextInputStyle.Short,
                                    Label: "Server name",
                                    MinLength: 1,
                                    MaxLength: default,
                                    IsRequired: true,
                                    Value: default,
                                    Placeholder: default
                                ),
                            }),
                        new ActionRowComponent(
                            new[]
                            {
                                new TextInputComponent(
                                    CustomID: "host",
                                    Style: TextInputStyle.Short,
                                    Label: "Server's hostname or IP address",
                                    MinLength: 1,
                                    MaxLength: default,
                                    IsRequired: true,
                                    Value: default,
                                    Placeholder: default
                                ),
                            }),
                        new ActionRowComponent(
                            new[]
                            {
                                new TextInputComponent(
                                    CustomID: "rcon-port",
                                    Style: TextInputStyle.Short,
                                    Label: "Port for RCON (usually 25575)",
                                    MinLength: 1,
                                    MaxLength: default,
                                    IsRequired: true,
                                    Value: "25575",
                                    Placeholder: default
                                ),
                            }),
                        new ActionRowComponent(
                            new[]
                            {
                                new TextInputComponent(
                                    CustomID: "rcon-password",
                                    Style: TextInputStyle.Short,
                                    Label: "RCON password",
                                    MinLength: 1,
                                    MaxLength: default,
                                    IsRequired: true,
                                    Value: default,
                                    Placeholder: default
                                ),
                            }),
                        new ActionRowComponent(
                            new[]
                            {
                                new TextInputComponent(
                                    CustomID: "ping-port",
                                    Style: TextInputStyle.Short,
                                    Label: "Minecraft port (usually 25565)",
                                    MinLength: 1,
                                    MaxLength: default,
                                    IsRequired: true,
                                    Value: "25565",
                                    Placeholder: default
                                ),
                            }),
                    }
                )
            ));

        return await _interactionApi.CreateInteractionResponseAsync(
            _interactionContext.Interaction.ID,
            _interactionContext.Interaction.Token,
            response,
            ct: CancellationToken);
    }
}

[Group("server")]
public sealed class ServerInteractionGroup : InteractionGroup
{
    private readonly FeedbackService _feedback;
    private readonly IInteractionContext _interactionContext;
    private readonly IMediator _mediator;

    public ServerInteractionGroup(FeedbackService feedback, IInteractionContext interactionContext, IMediator mediator)
    {
        _feedback = feedback;
        _interactionContext = interactionContext;
        _mediator = mediator;
    }

    [Modal("add")]
    public async Task<Result> OnServerAddAsync(string name,
        string host,
        ushort rconPort,
        string rconPassword,
        ushort pingPort)
    {
        if (!_interactionContext.TryGetGuildID(out var guildId))
        {
            return new InvalidOperationError("You must be in a guild to perform that action");
        }

        var addResult =
            await _mediator.Send(new AddServerCommand(guildId.Value, name, host, rconPort, rconPassword, pingPort));
        if (!addResult.IsSuccess) return addResult;

        return (Result)await _feedback.SendContextualSuccessAsync($"{name} at `{host}` added.");
    }
}

// TODO authorization and validation (eg strings aren't empty)
public sealed record AddServerCommand(
    Snowflake GuildId,
    string Name,
    string Host,
    ushort RconPort,
    string RconPassword,
    ushort PingPort) : IRequest<Result>;

public sealed partial class AddServerCommandHandler : IRequestHandler<AddServerCommand, Result>
{
    private readonly WoollyContext _db;
    private readonly ILogger<AddServerCommandHandler> _logger;

    public AddServerCommandHandler(WoollyContext db, ILogger<AddServerCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result> Handle(AddServerCommand request, CancellationToken ct)
    {
        _db.MinecraftServers.Add(new MinecraftServer
        {
            GuildId = request.GuildId,
            Name = request.Name,
            Host = request.Host,
            RconPort = request.RconPort,
            RconPassword = request.RconPassword,
            PingPort = request.PingPort,
        });

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (UniqueConstraintException)
        {
            // TODO should I use ArgumentInvalidError instead?
            return new InvalidOperationError($"You've already registered a server under the name `{request.Name}`.");
        }

        ServerAdded(request.Name, request.Host, request.GuildId);
        return Result.FromSuccess();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Minecraft server {Name} at {Host} added to {GuildId}")]
    private partial void ServerAdded(string name, string host, Snowflake guildId);
}
