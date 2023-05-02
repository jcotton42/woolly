using MediatR;

namespace Woolly.Infrastructure;

public class RequestIdLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly WoollyRequestContext _context;
    private readonly ILogger<RequestIdLoggingBehavior<TRequest, TResponse>> _logger;

    public RequestIdLoggingBehavior(WoollyRequestContext context,
        ILogger<RequestIdLoggingBehavior<TRequest, TResponse>> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope("Request ID: {RequestId}", _context.RequestId))
        {
            return await next();
        }
    }
}
