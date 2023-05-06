namespace Woolly.Features.Rcon;

public sealed class RconOptions
{
    public required string Hostname { get; init; }
    public required int Port { get; init; }
    public required string Password { get; init; }
}
