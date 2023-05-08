using Remora.Results;

namespace Woolly.Infrastructure;

public sealed record RateLimitError(string Message) : ResultError(Message);

public sealed record RetryError
    (string Message = "The operation did not succeed, and should be retried.") : ResultError(Message);
