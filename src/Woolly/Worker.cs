using System;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Woolly {
    public class Worker : BackgroundService {
        private readonly ILogger<Worker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DiscordOptions _discordOptions;

        public Worker(ILogger<Worker> logger, ILoggerFactory loggerFactory, IOptions<DiscordOptions> discordOptions) {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _discordOptions = discordOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            using var discord = new DiscordClient(new DiscordConfiguration {
                LoggerFactory = _loggerFactory,
                Token = _discordOptions.ApiToken,
                TokenType = TokenType.Bot,
            });

            discord.Ready += OnDiscordReady;
            discord.GuildAvailable += OnDiscordGuildAvailable;

            await discord.ConnectAsync();
            try {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            } catch(OperationCanceledException) {}
            await discord.DisconnectAsync();
        }

        private Task OnDiscordReady(DiscordClient sender, ReadyEventArgs e) {
            sender.Logger.LogInformation("Connected to Discord");
            return Task.CompletedTask;
        }

        private Task OnDiscordGuildAvailable(DiscordClient sender, GuildCreateEventArgs e) {
            sender.Logger.LogInformation($"Guild available: {e.Guild.Name}");
            return Task.CompletedTask;
        }
    }
}
