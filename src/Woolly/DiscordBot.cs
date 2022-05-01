using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Options;
using Woolly.Commands;

namespace Woolly; 

public class DiscordBot : BackgroundService {
    private static readonly EventId CommandErroredEventId = new(1, "CommandErrored");
    private static readonly EventId DiscordReadyEventId = new(2, "DiscordReady");
    private static readonly EventId GuideAvailableEventId = new(3, "GuildAvailable");
    private static readonly EventId InsufficientPermissionsEventId = new(4, "InsufficientPermissions");

    private readonly ILogger<DiscordBot> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly DiscordOptions _discordOptions;
    private readonly IServiceProvider _serviceProvider;

    public DiscordBot(ILogger<DiscordBot> logger, ILoggerFactory loggerFactory,
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
        commands.RegisterCommands<AdminCommandModule>();

        commands.CommandExecuted += OnCommandExecuted;
        commands.CommandErrored += OnCommandErrored;

        await discord.ConnectAsync();
        try {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        } catch(OperationCanceledException) {}
        await discord.DisconnectAsync();
    }

    private Task OnDiscordReady(DiscordClient sender, ReadyEventArgs e) {
        sender.Logger.LogInformation(DiscordReadyEventId, "Connected to Discord");
        return Task.CompletedTask;
    }

    private Task OnDiscordGuildAvailable(DiscordClient sender, GuildCreateEventArgs e) {
        sender.Logger.LogInformation(GuideAvailableEventId, "Guild available: {GuildName} (id: {GuildID})", e.Guild.Name, e.Guild.Id);
        return Task.CompletedTask;
    }

    private async Task OnCommandExecuted(CommandsNextExtension sender, CommandExecutionEventArgs e) {
        var emoji = DiscordEmoji.FromName(sender.Client, _discordOptions.GetOkEmoji(e.Context.Guild.Id));
        await e.Context.Message.CreateReactionAsync(emoji);
    }

    private async Task OnCommandErrored(CommandsNextExtension sender, CommandErrorEventArgs e) {
        if(e.Exception is CommandNotFoundException) {
            // command wasn't found, maybe react?
        } else if(e.Exception is ArgumentException) {
            var emoji = DiscordEmoji.FromName(sender.Client, _discordOptions.GetFailEmoji(e.Context.Guild.Id));
            await e.Context.Message.CreateReactionAsync(emoji);
        } else if(e.Exception is ChecksFailedException cfe) {
            if(cfe.FailedChecks.FirstOrDefault(check => check is RequireBotPermissionsAttribute) is RequireBotPermissionsAttribute attr) {
                sender.Client.Logger.LogError(
                    InsufficientPermissionsEventId,
                    "`{Command}` failed to execute in '{Guild}', requires permissions {Permissions}",
                    e.Context.Command.QualifiedName,
                    e.Context.Guild.Name,
                    attr.Permissions
                );
                await e.Context.RespondAsync("I can't do that :disappointed:");
            } else {
                var emoji = DiscordEmoji.FromName(sender.Client, _discordOptions.GetFailEmoji(e.Context.Guild.Id));
                await e.Context.Message.CreateReactionAsync(emoji);
                await e.Context.RespondAsync(
                    $"{e.Context.User.Mention} is not in the sudoers file. This incident will be reported.");
            }
        } else {
            sender.Client.Logger.LogError(
                CommandErroredEventId,
                e.Exception,
                "`{Command}` failed with exception:",
                e.Command.QualifiedName
            );
        }
    }
}
