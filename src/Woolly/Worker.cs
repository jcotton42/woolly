using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Woolly {
    public class Worker : BackgroundService {
        private readonly ILogger<Worker> _logger;
        private readonly DiscordOptions _discordOptions;

        public Worker(ILogger<Worker> logger, IOptions<DiscordOptions> discordOptions) {
            _logger = logger;
            _discordOptions = discordOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            while(!stoppingToken.IsCancellationRequested) {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                _logger.LogInformation("Discord token: {token}", _discordOptions.ApiToken);

                foreach(var (key, value) in _discordOptions.GuildOptions) {
                    _logger.LogInformation("Guild {guild} with ok emoji {ok} and fail emoji {fail}", key, value.OkEmoji, value.FailEmoji);
                }
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
