using System;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Woolly.Commands;

namespace Woolly {
    public class Worker : BackgroundService {
        private readonly ILogger<Worker> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DiscordOptions _discordOptions;
        private readonly IServiceProvider _serviceProvider;

        public Worker(ILogger<Worker> logger, ILoggerFactory loggerFactory,
            IOptions<DiscordOptions> discordOptions, IServiceProvider serviceProvider) {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _discordOptions = discordOptions.Value;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            using var discord = new DiscordClient(new DiscordConfiguration {
                LoggerFactory = _loggerFactory,
                Token = _discordOptions.ApiToken,
                TokenType = TokenType.Bot,
            });

            discord.Ready += OnDiscordReady;
            discord.GuildAvailable += OnDiscordGuildAvailable;

            var commands = discord.UseCommandsNext(new CommandsNextConfiguration {
                Services = _serviceProvider,
                StringPrefixes = _discordOptions.CommandPrefixes,
            });
            commands.RegisterCommands<MinecraftCommandModule>();

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
