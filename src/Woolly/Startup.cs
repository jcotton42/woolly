using Microsoft.Extensions.Options;

using Remora.Discord.Commands.Services;

namespace Woolly;

public sealed partial class Startup : BackgroundService
{
    private readonly DiscordOptions _discordOptions;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<Startup> _logger;
    private readonly SlashService _slash;

    public Startup(IOptions<DiscordOptions> discordOptions,
        IHostApplicationLifetime lifetime,
        ILogger<Startup> logger,
        SlashService slash)
    {
        _discordOptions = discordOptions.Value;
        _lifetime = lifetime;
        _logger = logger;
        _slash = slash;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var result = await _slash.UpdateSlashCommandsAsync(_discordOptions.TestServerId, ct: stoppingToken);

        if (result.IsSuccess)
        {
            CommandsUpdated();
            JoinLink(
                $"https://discord.com/api/oauth2/authorize?client_id={_discordOptions.AppId}&permissions=2147485696&scope=bot%20applications.commands");
            return;
        }

        CommandUpdateFailed(result.Error.Message);
        _lifetime.StopApplication();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Discord commands updated")]
    private partial void CommandsUpdated();

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Join link: {JoinLink}")]
    private partial void JoinLink(string joinLink);

    [LoggerMessage(EventId = 100, Level = LogLevel.Critical,
        Message = "Failed to update Discord commands, aborting. Cause: {Message}")]
    private partial void CommandUpdateFailed(string message);
}

public static class StartupServiceCollectionExtensions
{
    public static IServiceCollection AddStartup(this IServiceCollection services)
        => services.AddHostedService<Startup>();
}
