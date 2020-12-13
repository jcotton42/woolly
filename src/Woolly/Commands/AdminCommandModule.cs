using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Woolly.Commands {
    [Group("admin")]
    public class AdminCommandModule : BaseCommandModule {
        private static readonly EventId ShutdownRequestedEventId = new EventId(1, "ShutdownRequested");
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public AdminCommandModule(ILogger<AdminCommandModule> logger, IHostApplicationLifetime hostApplicationLifetime) {
            _logger = logger;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        [Command("shutdown")]
        [Description("Shuts down the bot")]
        [RequireOwner]
        public async Task ShutdownCommand(CommandContext ctx) {
            _logger.LogInformation(ShutdownRequestedEventId,
                "{user} requested a bot shutdown.", $"{ctx.User.Username}#{ctx.User.Discriminator}");
            await ctx.RespondAsync("Farewell!");
            _hostApplicationLifetime.StopApplication();
        }
    }
}
