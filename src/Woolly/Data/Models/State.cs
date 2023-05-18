using NodaTime;

namespace Woolly.Data.Models;

public sealed class State
{
    public int Id { get; init; }
    public required string Data { get; init; }
    // TODO recurring job to expunge expired states
    public required Instant ExpiryTime { get; init; }
}
