using FuzzySharp;

using Microsoft.EntityFrameworkCore;

using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Autocomplete;

using Woolly.Data;

namespace Woolly.Infrastructure;

public sealed class MinecraftServerNameAutocompleter : IAutocompleteProvider
{
    private readonly WoollyRequestContext _context;
    private readonly WoollyContext _db;

    public MinecraftServerNameAutocompleter(WoollyRequestContext context, WoollyContext db)
    {
        _context = context;
        _db = db;
    }

    public const string Identity = "autocomplete::minecraft_server_name";
    string IAutocompleteProvider.Identity => Identity;

    public async ValueTask<IReadOnlyList<IApplicationCommandOptionChoice>> GetSuggestionsAsync(
        IReadOnlyList<IApplicationCommandInteractionDataOption> options,
        string userInput,
        CancellationToken ct)
    {
        if (_context.InteractionContext?.GuildId is not { } guildId)
        {
            return Array.Empty<IApplicationCommandOptionChoice>();
        }

        var servers = await _db.MinecraftServers
            .Where(ms => ms.GuildId == guildId)
            .Select(ms => ms.Name)
            .ToListAsync(ct);

        return servers
            .OrderByDescending(server => Fuzz.Ratio(userInput, server))
            .Take(25)
            .Select(server => new ApplicationCommandOptionChoice(server, server))
            .ToList();
    }
}
