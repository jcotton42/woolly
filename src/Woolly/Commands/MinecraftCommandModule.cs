using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace Woolly.Commands {
    public class MinecraftCommandModule : BaseCommandModule {
        private readonly ILogger _logger;

        public MinecraftCommandModule(ILogger<MinecraftCommandModule> logger) {
            _logger = logger;
        }

        [Command("greet")]
        public async Task GreetCommand(CommandContext ctx, DiscordMember member) {
            await ctx.RespondAsync($"Hello {member.Mention}!");
        }
    }
}
