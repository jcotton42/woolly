using MediatR;

using Remora.Commands.Attributes;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Extensions.Embeds;
using Remora.Rest.Core;
using Remora.Results;

using Woolly.Infrastructure;

namespace Woolly.Features.ServerListPing;

public sealed partial class ServerListPingCommands
{
    [Command("status")]
    public async Task<IResult> Status([AutocompleteProvider(MinecraftServerNameAutocompleter.Identity)] string server)
    {
        var result =
            await _mediator.Send(new StatusQuery(_requestContext.InteractionContext!.Value.GuildId!.Value, server));
        if (!result.IsDefined(out var status)) return result;

        // TODO maybe resolve this to online usernames
        // probably should use a DTO from the handler actually
        var title = $"Players ({status.Players.Online}/{status.Players.Max})";
        var playerField = status.Players.Online > 0
            ? new EmbedField(title, string.Join(", ", status.Players.Players!.Select(p => p.Name)))
            : new EmbedField(title, "No one is online :cry:");

        var embed = new EmbedBuilder()
            .WithTitle($"{server} ({status.Version.Name})")
            .WithDescription(status.Description.Text!)
            // TODO just passing the data URL doesn't work, an attachment will need to be added
            // see the below, using Velvet's Remora.Discord.Builders
            // var message = new MessageBuilder().AddAttachment(myFileStream, "my_image.png").AddEmbed(
            //        new Embed { Image = new EmbedImage("attachment://my_image.png") } );
            //.WithThumbnailUrl(status.Favicon!)
            .AddField(playerField).Entity
            .Build().Entity;

        return await _feedback.SendContextualEmbedAsync(embed, ct: CancellationToken);
    }
}

public sealed record StatusQuery(Snowflake GuildId, string ServerName) : IRequest<Result<ServerStatus>>;

public sealed class StatusQueryHandler : IRequestHandler<StatusQuery, Result<ServerStatus>>
{
    private readonly ServerListPingClientFactory _pingClientFactory;

    public StatusQueryHandler(ServerListPingClientFactory pingClientFactory) => _pingClientFactory = pingClientFactory;

    public async Task<Result<ServerStatus>> Handle(StatusQuery request, CancellationToken cancellationToken)
    {
        var getClient = await _pingClientFactory.GetClientAsync(request.GuildId, request.ServerName, cancellationToken);
        if (!getClient.IsDefined(out var client)) return Result<ServerStatus>.FromError(getClient);

        return await client.GetStatusAsync(cancellationToken);
    }
}
