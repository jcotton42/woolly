using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Woolly.Services;

namespace Woolly.Commands {
    [Group("mc")]
    [Description("Minecraft commands")]
    public class MinecraftCommandModule : BaseCommandModule {
        private readonly ILogger _logger;
        private readonly IMinecraftClientFactory _clientFactory;

        public MinecraftCommandModule(ILogger<MinecraftCommandModule> logger, IMinecraftClientFactory clientFactory) {
            _logger = logger;
            _clientFactory = clientFactory;
        }

        [Command("list")]
        [Description("Lists available Minecraft servers")]
        public async Task ListServersCommand(CommandContext ctx) {
            await ctx.RespondAsync(string.Join(", ", _clientFactory.Servers));
        }

        [Group("whitelist")]
        [Description("Whitelisting of users")]
        public class MinecraftWhitelistCommandModule : BaseCommandModule {
            private readonly ILogger _logger;
            private readonly IMinecraftClientFactory _clientFactory;

            public MinecraftWhitelistCommandModule(ILogger<MinecraftWhitelistCommandModule> logger, IMinecraftClientFactory clientFactory) {
                _logger = logger;
                _clientFactory = clientFactory;
            }

            [Command("add")]
            [Description("Adds a user to the whitelist")]
            public async Task AddCommand(CommandContext ctx, string server, string user) {
                var client = await _clientFactory.GetClientAsync(server);
                if(client is null) {
                    return;
                }
                await client.AddWhitelistAsync(user);
            }

            [Command("remove")]
            [Description("Removes a user from the whitelist")]
            public async Task RemoveCommand(CommandContext ctx, string server, string user) {
                var client = await _clientFactory.GetClientAsync(server);
                if(client is null) {
                    return;
                }
                await client.RemoveWhitelistAsync(user);
            }

            [Command("list")]
            [Description("Lists the users on the whitelist")]
            public async Task ListCommand(CommandContext ctx, string server) {
                var client = await _clientFactory.GetClientAsync(server);
                if(client is null) {
                    return;
                }
                await ctx.RespondAsync(string.Join(", ", await client.ListWhitelistAsync()));
            }
        }
    }
}
