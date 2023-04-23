using Microsoft.Extensions.Options;

namespace Woolly.Rcon;

public sealed class MinecraftRconClient
{
    private static readonly string[] WhitelistSplits = {":", ", ", " and "};

    private readonly RconClient _client;
    private readonly MinecraftRconOptions _options;

    public MinecraftRconClient(RconClient client, IOptions<MinecraftRconOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<bool> AddToWhitelistAsync(string username, CancellationToken token)
    {
        await EnsureConnectedAsync(token);
        var result = await _client.SendCommandAsync($"whitelist add {username}", token);
        return result.StartsWith("added", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> RemoveWhitelistAsync(string username, CancellationToken token)
    {
        await EnsureConnectedAsync(token);
        var result = await _client.SendCommandAsync($"whitelist remove {username}", token);
        return result.StartsWith("removed", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<List<string>> ListWhitelistAsync(CancellationToken token)
    {
        await EnsureConnectedAsync(token);
        var response = await _client.SendCommandAsync("whitelist list", token);
        return response
            .Split(WhitelistSplits, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1) // skip the part before the colon
            .ToList();
    }

    public async Task SayAsync(string message, CancellationToken token)
    {
        await EnsureConnectedAsync(token);
        await _client.SendCommandAsync($"say {message}", token);
    }

    private async Task EnsureConnectedAsync(CancellationToken token)
    {
        if (_client.IsConnected) return;
        await _client.ConnectAsync(_options.Hostname, _options.Port, _options.Password, token);
    }
}
