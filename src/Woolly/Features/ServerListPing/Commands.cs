using MediatR;

using Remora.Commands.Groups;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Feedback.Services;

using Woolly.Infrastructure;

namespace Woolly.Features.ServerListPing;

[RequireContext(ChannelContext.Guild)]
public sealed partial class ServerListPingCommands : CommandGroup
{
    private readonly FeedbackService _feedback;
    private readonly IMediator _mediator;
    private readonly WoollyRequestContext _requestContext;

    public ServerListPingCommands(FeedbackService feedback, IMediator mediator, WoollyRequestContext requestContext)
    {
        _feedback = feedback;
        _mediator = mediator;
        _requestContext = requestContext;
    }
}
