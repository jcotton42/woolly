using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
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
            private readonly DiscordOptions _discordOptions;

            public MinecraftWhitelistCommandModule(ILogger<MinecraftWhitelistCommandModule> logger,
                IMinecraftClientFactory clientFactory, IOptions<DiscordOptions> discordOptions) {
                _logger = logger;
                _clientFactory = clientFactory;
                _discordOptions = discordOptions.Value;
            }

            [Command("add")]
            [Description("Adds a user to the whitelist")]
            public async Task AddCommand(CommandContext ctx, string user) {
                await ctx.RespondAsync($"This command would add {user} to the default server's whitelist");
            }

            [Command("add")]
            public async Task AddCommand(CommandContext ctx, string server, string minecraftUser, DiscordMember guildMember) {
                var client = await _clientFactory.GetClientAsync(server);
                if(client is null) {
                    return;
                }

                if(await client.AddWhitelistAsync(minecraftUser)) {
                    if(_discordOptions.GetMinecraftRoleID(ctx.Guild.Id) is ulong roleId) {
                        if((ctx.Guild.Permissions & Permissions.ManageRoles) != 0) {
                            var role = ctx.Guild.GetRole(roleId);
                            await guildMember.GrantRoleAsync(role, "Whitelisting for Minecraft server");
                        }
                    }
                } else {
                    throw new ArgumentException($"Could not add {minecraftUser} to the whitelist.", nameof(minecraftUser));
                }
            }

            [Command("remove")]
            [Description("Removes a user from the whitelist")]
            public async Task RemoveCommand(CommandContext ctx, string user) {
                await ctx.RespondAsync($"This command would remove {user} from the default server's whitelist");
            }

            [Command("remove")]
            public async Task RemoveCommand(CommandContext ctx, string server, string minecraftUser, DiscordMember guildMember) {
                var client = await _clientFactory.GetClientAsync(server);
                if(client is null) {
                    return;
                }
                if(await client.RemoveWhitelistAsync(minecraftUser)) {
                    if(_discordOptions.GetMinecraftRoleID(ctx.Guild.Id) is ulong roleId) {
                        if((ctx.Guild.Permissions & Permissions.ManageRoles) != 0) {
                            var role = ctx.Guild.GetRole(roleId);
                            await guildMember.RevokeRoleAsync(role, "De-whitelisting for Minecraft server");
                        }
                    }
                } else {
                    throw new ArgumentException($"Could not remove {minecraftUser} from the whitelist.", nameof(minecraftUser));
                }
            }

            [Command("list")]
            [Description("Lists the users on the whitelist")]
            public async Task ListCommand(CommandContext ctx) {
                await ctx.RespondAsync("This command would show the whitelist for the default server");
            }

            [Command("list")]
            public async Task ListCommand(CommandContext ctx, string server) {
                var client = await _clientFactory.GetClientAsync(server);
                if(client is null) {
                    return;
                }
                var whitelist = await client.ListWhitelistAsync();
                if(whitelist.Any()) {
                    await ctx.RespondAsync(string.Join(", ", whitelist));
                } else {
                    await ctx.RespondAsync("Whitelist is empty.");
                }
            }
        }
    }
}
