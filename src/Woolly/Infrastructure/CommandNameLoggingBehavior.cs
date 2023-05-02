using MediatR;

using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Services;

namespace Woolly.Infrastructure;

public class CommandNameLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ContextInjectionService _contextInjector;
    private readonly ILogger<CommandNameLoggingBehavior<TRequest, TResponse>> _logger;

    public CommandNameLoggingBehavior(ContextInjectionService contextInjector,
        ILogger<CommandNameLoggingBehavior<TRequest, TResponse>> logger)
    {
        _contextInjector = contextInjector;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (_contextInjector.Context is not ICommandContext commandContext) return await next();
        using (_logger.BeginScope("Executing `{CommandName}`", commandContext.Command.Command.Node.Key))
        {
            return await next();
        }
    }
}
