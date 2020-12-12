using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Woolly.Services {
    public interface IMinecraftClientFactory {
        Task<MinecraftClient?> GetClientAsync(string nickname);
        IReadOnlyList<string> Servers { get; }
    }

    public class MinecraftClientFactory : IMinecraftClientFactory, IDisposable {
        private readonly Dictionary<string, MinecraftClient> _clients;

        public IReadOnlyList<string> Servers { get; }

        public MinecraftClientFactory(ILoggerFactory loggerFactory, IOptions<MinecraftOptions> minecraftOptions) {
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

        public async Task<MinecraftClient?> GetClientAsync(string nickname) {
            if(_clients.TryGetValue(nickname, out var client)) {
                if(!client.IsConnected) {
                    await client.ConnectAsync();
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
}
