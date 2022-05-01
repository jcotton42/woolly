using Microsoft.Extensions.Options;
using Woolly;
using Woolly.Services;

var invalidOptionsEventId = new EventId(1, "InvalidOptions");
var unexpectedTerminationEventId = new EventId(2, "UnexpectedTermination");

var host = Host
    .CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(configuration => {
        configuration.AddKeyPerFile("/run/secrets", optional: true);
    })
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
    })
    .Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Woolly");

try {
    host.Run();
    return 0;
} catch(OptionsValidationException e) {
    logger.LogCritical(
        invalidOptionsEventId,
        "Host terminated due to invalid configuration: {@Errors}.",
        e.Failures
    );
    return 1;
} catch(Exception e) {
    logger.LogCritical(unexpectedTerminationEventId, e, "Host terminated unexpectedly.");
    return 2;
}
