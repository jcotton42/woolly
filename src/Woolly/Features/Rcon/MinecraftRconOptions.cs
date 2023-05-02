namespace Woolly.Features.Rcon;

public sealed class MinecraftRconOptions
{
    public string Hostname { get; set; } = null!;
    public int Port { get; set; }
    public string Password { get; set; } = null!;
}
