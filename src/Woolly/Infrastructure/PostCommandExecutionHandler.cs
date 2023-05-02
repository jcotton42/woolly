using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Commands.Services;
using Remora.Results;

namespace Woolly.Infrastructure;

public sealed partial class PostCommandExecutionHandler : IPostExecutionEvent
{
    private readonly WoollyRequestContext _requestContext;
    private readonly FeedbackService _feedback;
    private readonly ILogger<PostCommandExecutionHandler> _logger;

    public PostCommandExecutionHandler(WoollyRequestContext requestContext,
        FeedbackService feedback,
        ILogger<PostCommandExecutionHandler> logger)
    {
        _requestContext = requestContext;
        _feedback = feedback;
        _logger = logger;
    }

    public async Task<Result> AfterExecutionAsync(ICommandContext commandContext,
        IResult commandResult,
        CancellationToken ct)
    {
        if (commandResult.IsSuccess) return Result.FromSuccess();
        // TODO add a way to distinguish between errors that should be reported to end users, and those that should just show the ID
        // TODO always show the request ID regardless, maybe there's like a footer thing I could use?
        // (maybe not?) [I'd say the former should inherit from a PublicErrorResult or similar
        var exception = commandResult.Error is ExceptionError ee
            ? ee.Exception
            : null;
        CommandFailed(exception, _requestContext.RequestId, commandContext.Command.Command.Node.Key, commandResult.Error.Message);
        return (Result)await _feedback.SendContextualErrorAsync(
            $"Something went wrong! Have the bot owner check the logs for request ID {_requestContext.RequestId}",
            ct: ct);
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Error,
        Message = "Request ID {RequestId}, command: `{CommandName}` failed: {Message}")]
    private partial void CommandFailed(Exception? exception, string requestId, string commandName, string message);
}
