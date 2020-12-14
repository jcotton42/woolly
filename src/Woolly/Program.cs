using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using Woolly.Services;

namespace Woolly {
    public class Program {
        private static readonly EventId InvalidOptionsEventId = new EventId(1, "InvalidOptions");
        private static readonly EventId UnexpectedTerminationEventId = new EventId(2, "UnexpectedTermination");

        public static int Main(string[] args) {
            var host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetService<ILogger<Program>>();

            try {
                host
                    // eager validation hack
                    // https://github.com/dotnet/runtime/issues/36391#issuecomment-631089093
                    .ValidateOptions<MinecraftOptions>()
                    .ValidateOptions<DiscordOptions>()
                    .Run();
                return 0;
            } catch(OptionsValidationException e) {
                logger.LogCritical(
                    InvalidOptionsEventId,
                    "Host terminated due to invalid configuration: {errors}.",
                    e.Failures
                );
                return 1;
            } catch(Exception e) {
                logger.LogCritical(UnexpectedTerminationEventId, e, "Host terminated unexpectedly.");
                return 2;
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureServices((hostContext, services) => {
                    services.AddOptions<DiscordOptions>()
                        .Bind(hostContext.Configuration.GetSection(DiscordOptions.SectionName));
                    services.AddSingleton<IValidateOptions<DiscordOptions>, DiscordOptionsValidator>();

                    services.AddOptions<MinecraftOptions>()
                        .Bind(hostContext.Configuration.GetSection(MinecraftOptions.SectionName));
                    services.AddSingleton<IValidateOptions<MinecraftOptions>, MinecraftOptionsValidator>();

                    services.AddSingleton<IMinecraftClientFactory, MinecraftClientFactory>();
                    services.AddHostedService<DiscordBot>();
                });
    }

    public static class OptionsBuilderValidationExtensions {
        public static IHost ValidateOptions<T>(this IHost host) where T: class {
            var options = host.Services.GetService<IOptions<T>>();
            if(options is not null) {
                // retrieval triggers validation
                // this is hack until https://github.com/dotnet/runtime/issues/36391 is closed
                var optionsValue = options.Value;
            }
            return host;
        }
    }
}
