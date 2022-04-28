using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace Woolly.Commands; 

public class AdminCommandModule : BaseCommandModule {
    private static readonly EventId ShutdownRequestedEventId = new EventId(1, "ShutdownRequested");
    private readonly ILogger _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public AdminCommandModule(ILogger<AdminCommandModule> logger, IHostApplicationLifetime hostApplicationLifetime) {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    [Command("cleanup")]
    [Description("Deletes bot messages in the current channel")]
    [RequireBotPermissions(Permissions.ManageMessages | Permissions.ReadMessageHistory)]
    public async Task CleanupCommand(CommandContext ctx, int limit = 100) {
        var messages = (await ctx.Channel.GetMessagesAsync(limit))
            .Where(m => m.Author.Id == ctx.Client.CurrentUser.Id).ToList();
        var count = messages.Count;
        await ctx.Channel.DeleteMessagesAsync(messages, "Deleted by cleanup command.");
        var respone = await ctx.RespondAsync($"Deleted {count} messages.");
        await Task.Delay(TimeSpan.FromSeconds(5));
        await respone.DeleteAsync();
    }

    [Command("shutdown")]
    [Description("Shuts down the bot")]
    [RequireOwner]
    public async Task ShutdownCommand(CommandContext ctx) {
        _logger.LogInformation(ShutdownRequestedEventId,
            "{User} requested a bot shutdown.", $"{ctx.User.Username}#{ctx.User.Discriminator}");
        await ctx.RespondAsync("Farewell!");
        _hostApplicationLifetime.StopApplication();
    }
}
