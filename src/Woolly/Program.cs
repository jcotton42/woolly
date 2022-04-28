using Microsoft.Extensions.Options;
using Woolly.Services;

namespace Woolly; 

public class Program {
    private static readonly EventId InvalidOptionsEventId = new EventId(1, "InvalidOptions");
    private static readonly EventId UnexpectedTerminationEventId = new EventId(2, "UnexpectedTermination");

    public static int Main(string[] args) {
        var host = CreateHostBuilder(args).Build();
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        try {
            host.Run();
            return 0;
        } catch(OptionsValidationException e) {
            logger.LogCritical(
                InvalidOptionsEventId,
                "Host terminated due to invalid configuration: {@Errors}.",
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
            .ConfigureServices((hostContext, services) => {
                services.AddOptions<DiscordOptions>()
                    .Bind(hostContext.Configuration.GetSection(DiscordOptions.SectionName))
                    .ValidateOnStart();
                services.AddSingleton<IValidateOptions<DiscordOptions>, DiscordOptionsValidator>();

                services.AddOptions<MinecraftOptions>()
                    .Bind(hostContext.Configuration.GetSection(MinecraftOptions.SectionName))
                    .ValidateOnStart();
                services.AddSingleton<IValidateOptions<MinecraftOptions>, MinecraftOptionsValidator>();

                services.AddSingleton<IMinecraftClientFactory, MinecraftClientFactory>();
                services.AddHostedService<DiscordBot>();
            });
}
