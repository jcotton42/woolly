using MediatR;

using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;

namespace Woolly.Features.Servers;

[Group("server")]
public sealed partial class ServerCommands : CommandGroup
{
    private readonly FeedbackService _feedback;
    private readonly IDiscordRestInteractionAPI _interactionApi;
    private readonly IInteractionContext _interactionContext;
    private readonly IMediator _mediator;

    public ServerCommands(
        FeedbackService feedback,
        IDiscordRestInteractionAPI interactionApi,
        IInteractionContext interactionContext,
        IMediator mediator)
    {
        _feedback = feedback;
        _interactionApi = interactionApi;
        _interactionContext = interactionContext;
        _mediator = mediator;
    }
}
