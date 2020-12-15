using Microsoft.Extensions.Logging;
using RconSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Woolly.Services {
    public sealed class MinecraftClient : IDisposable {
        private static readonly EventId ConnectedEventId = new EventId(1, "MinecraftConnected");
        private static readonly EventId AuthFailedEventId = new EventId(2, "MinecraftAuthFailed");
        private readonly ILogger _logger;
        private readonly RconClient _rcon;
        private readonly string _nickname;
        private readonly string _host;
        private readonly ushort _rconPort;
        private readonly ushort _queryPort;
        private readonly string _rconPassword;

        public bool IsConnected { get; private set; } = false;

        public MinecraftClient(ILogger<MinecraftClient> logger,
            string nickname, string host, ushort rconPort, ushort queryPort,
            string rconPassword) {
            _logger = logger;
            _nickname = nickname;
            _host = host;
            _rconPort = rconPort;
            _queryPort = queryPort;
            _rconPassword = rconPassword;
            _rcon = RconClient.Create(host, rconPort);
        }

        /// <summary>
        /// Attempts to connect and authenticate to the RCON listener.
        /// </summary>
        /// <returns>Whether connection and auth were successful.</returns>
        internal async Task<bool> TryConnectAsync() {
            if(IsConnected) {
                return true;
            }
            await _rcon.ConnectAsync();
            if(await _rcon.AuthenticateAsync(_rconPassword)) {
                _logger.LogInformation(ConnectedEventId,
                    "Connected to '{Nickname}' RCON, at host {Host} on port {Port}.", _nickname, _host, _rconPort);
                IsConnected = true;
                return true;
            } else {
                _logger.LogError(AuthFailedEventId,
                    "Authentication to '{Nickname}' RCON at host {Host} on port {Port} failed.", _nickname, _host, _rconPort);
                return false;
            }
        }

        /// <summary>
        /// Adds a user to the whitelist.
        /// </summary>
        /// <param name="username">The Minecraft username to whitelist.</param>
        /// <returns>
        /// <para>Whether the user was successfully whitelisted.</para>
        /// <para>Note <c>true</c> is also returned for users already on the whitelist.</para>
        /// </returns>
        public async Task<bool> AddWhitelistAsync(string username) {
            var result = await _rcon.ExecuteCommandAsync($"whitelist add {username}");
            return result.StartsWith("added", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Removes a user from the whitelist.
        /// </summary>
        /// <param name="username">The Minecraft username to remove.</param>
        /// <returns>
        /// <para>Whether the user was successfully removed.</para>
        /// <para>Note that <c>false</c> is also returned for users not on the whitelist.</para>
        /// </returns>
        public async Task<bool> RemoveWhitelistAsync(string username) {
            var result = await _rcon.ExecuteCommandAsync($"whitelist remove {username}");
            return result.StartsWith("removed", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets all the users on the whitelist.
        /// </summary>
        /// <returns>The contents of the whitelist.</returns>
        public async Task<List<string>> ListWhitelistAsync() {
            // `whitelist list` format
            // "a"
            // "a and b"
            // "a, b and c"
            // because god forbid we just use commas
            var response = (await _rcon.ExecuteCommandAsync("whitelist list")).Split(":", 2)[1];

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

        /// <summary>
        /// Broadcasts a message to all users on the server.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public async Task SayAsync(string message) {
            await _rcon.ExecuteCommandAsync($"say {message}");
        }

        /// <summary>
        /// Gets all the players currently online.
        /// </summary>
        /// <returns>The online players.</returns>
        public async Task<List<string>> GetOnlinePlayers() {
            throw new NotImplementedException();
        }

        public void Dispose() {
            _rcon.Disconnect();
        }
    }
}
