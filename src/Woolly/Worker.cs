using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Woolly {
    public class Worker : BackgroundService {
        private readonly ILogger<Worker> _logger;
        private string _discordToken;

        public Worker(ILogger<Worker> logger, IConfiguration configuration) {
            _logger = logger;
            _discordToken = configuration["Discord:ApiToken"];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            while(!stoppingToken.IsCancellationRequested) {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                _logger.LogInformation("Discord token: {token}", _discordToken);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
