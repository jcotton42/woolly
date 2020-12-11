using CoreRCON;
using CoreRCON.PacketFormats;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Woolly.Services {
    public sealed class MinecraftClient : IDisposable {
        private static readonly EventId ConnectedEventId = new EventId(1, "MinecraftConnected");
        private readonly ILogger _logger;
        private readonly RCON _rcon;
        private readonly string _nickname;
        private readonly string _host;
        private readonly ushort _rconPort;
        private readonly ushort _queryPort;
        private bool _connected = false;

        public MinecraftClient(ILogger<MinecraftClient> logger,
            string nickname, string host, ushort rconPort, ushort queryPort,
            string rconPassword) {
            _logger = logger;
            _nickname = nickname;
            _host = host;
            _rconPort = rconPort;
            _queryPort = queryPort;
            _rcon = new RCON(IPAddress.Parse(host), rconPort, rconPassword);
        }

        public async Task ConnectAsync() {
            if(_connected) {
                return;
            }
            await _rcon.ConnectAsync();
            _logger.LogInformation(ConnectedEventId,
                "Connected to '{nickname}' RCON, at host {host} on port {port}", _nickname, _host, _rconPort);
            _connected = true;
        }

        public async Task<bool> AddWhitelistAsync(string user) {
            var result = await _rcon.SendCommandAsync($"whitelist add {user}");
            return result.StartsWith("added", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> RemoveWhitelistAsync(string user) {
            var result = await _rcon.SendCommandAsync($"whitelist remove {user}");
            return result.StartsWith("removed", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<string>> ListWhitelistAsync() {
            // `whitelist list` format
            // "a"
            // "a and b"
            // "a, b and c"
            // because god forbid we just use commas
            var response = (await _rcon.SendCommandAsync("whitelist list")).Split(":", 2)[1];

            var temp = response.Split(" and ");
            if(string.IsNullOrWhiteSpace(temp[0])) {
                return new List<string>();
            }
            var users = new List<string>(temp[0].Split(", "));
            if(temp.Length > 1) {
                users.Add(temp[1]);
            }
            return users;
        }

        public async Task SayAsync(string message) {
            await _rcon.SendCommandAsync($"say {message}");
        }

        public async Task<List<string>> GetOnlinePlayers() {
            var info = await ServerQuery.Info(IPAddress.Parse(_host), _queryPort, ServerQuery.ServerType.Minecraft) as MinecraftQueryInfo;
            return info.Players.ToList();
        }

        public void Dispose() {
            _rcon.Dispose();
        }
    }
}
