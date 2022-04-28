using Microsoft.Extensions.Options;

namespace Woolly.Services; 

public interface IMinecraftClientFactory {
    Task<MinecraftClient?> GetClientAsync(string nickname);
    IReadOnlyList<string> Servers { get; }
}

public class MinecraftClientFactory : IMinecraftClientFactory, IDisposable {
    private readonly ILogger _logger;
    private readonly Dictionary<string, MinecraftClient> _clients;

    public IReadOnlyList<string> Servers { get; }

    public MinecraftClientFactory(ILogger<MinecraftClientFactory> logger, ILoggerFactory loggerFactory,
        IOptions<MinecraftOptions> minecraftOptions) {
        _logger = logger;

        var servers = new List<string>();
        _clients = new Dictionary<string, MinecraftClient>(StringComparer.OrdinalIgnoreCase);
        foreach(var (nickname, server) in minecraftOptions.Value.Servers) {
            _clients.Add(
                nickname,
                new MinecraftClient(
                    loggerFactory.CreateLogger<MinecraftClient>(),
                    nickname,
                    server.Host,
                    server.RconPort,
                    server.QueryPort,
                    server.RconPassword
                )
            );
            servers.Add(nickname);
        }
        Servers = servers.AsReadOnly();
    }

    /// <summary>
    /// Gets the client for the server with the associated nickname.
    /// </summary>
    /// <param name="nickname">The server's nickname.</param>
    /// <returns>The client, if it matches a known server.</returns>
    public async Task<MinecraftClient?> GetClientAsync(string nickname) {
        if(_clients.TryGetValue(nickname, out var client)) {
            if(!client.IsConnected) {
                if(await client.TryConnectAsync()) {
                    return client;
                } else {
                    return null;
                }
            }
            return client;
        } else {
            return null;
        }
    }

    public void Dispose() {
        foreach(var client in _clients.Values) {
            client.Dispose();
        }
    }
}
