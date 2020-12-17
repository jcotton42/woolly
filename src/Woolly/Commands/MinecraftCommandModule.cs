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
            if(_clientFactory.Servers.Any()) {
                var servers = string.Join(", ", _clientFactory.Servers);
                await ctx.RespondAsync($"Available servers: {servers}");
            } else {
                await ctx.RespondAsync("No servers are configured.");
            }
        }

        [Group("whitelist")]
        [Aliases("wl")]
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
            [RequirePermissions(Permissions.ManageRoles)]
            public async Task AddCommand(CommandContext ctx, string minecraftUser, DiscordMember guildMember) {
                if(_discordOptions.GetDefaultMinecraftServer(ctx.Guild.Id) is string server) {
                    await AddCommand(ctx, server, minecraftUser, guildMember);
                } else {
                    await ctx.RespondAsync("This guild has no configured default Minecraft server.");
                    throw new ArgumentException(nameof(server));
                }
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
            [Aliases("rm")]
            [Description("Removes a user from the whitelist")]
            [RequirePermissions(Permissions.ManageRoles)]
            public async Task RemoveCommand(CommandContext ctx, string minecraftUser, DiscordMember guildMember) {
                if(_discordOptions.GetDefaultMinecraftServer(ctx.Guild.Id) is string server) {
                    await RemoveCommand(ctx, server, minecraftUser, guildMember);
                } else {
                    await ctx.RespondAsync("This guild has no configured default Minecraft server.");
                    throw new ArgumentException(nameof(server));
                }
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
            [Aliases("ls")]
            [Description("Lists the users on the whitelist")]
            public async Task ListCommand(CommandContext ctx) {
                if(_discordOptions.GetDefaultMinecraftServer(ctx.Guild.Id) is string server) {
                    await ListCommand(ctx, server);
                } else {
                    await ctx.RespondAsync("This guild has no configured default Minecraft server.");
                    throw new ArgumentException(nameof(server));
                }
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
