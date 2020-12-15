using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace Woolly {
    public class DiscordOptions {
        // null! tells the compiler to assume the field isn't null
        // validation will take care of that for us
        public const string SectionName = "Discord";

        public string ApiToken { get; set; } = null!;
        public string[]? CommandPrefixes { get; set; }
        public Dictionary<string, GuildOptions>? GuildOptions { get; set; }

        public string GetOkEmoji(ulong guildID) {
            if(GuildOptions is not null && GuildOptions.TryGetValue(guildID.ToString(), out var guildOptions)) {
                return guildOptions.OkEmoji ?? ":ok_hand:";
            }
            return ":ok_hand:";
        }

        public string GetFailEmoji(ulong guildID) {
            if(GuildOptions is not null && GuildOptions.TryGetValue(guildID.ToString(), out var guildOptions)) {
                return guildOptions.FailEmoji ?? ":no_entry:";
            }

            return ":no_entry:";
        }

        public ulong? GetMinecraftRoleID(ulong guildID) {
            if(GuildOptions is not null && GuildOptions.TryGetValue(guildID.ToString(), out var guildOptions)) {
                return guildOptions.MinecraftRoleID;
            }

            return null;
        }
    }

    public class GuildOptions {
        public string? OkEmoji { get; set; }
        public string? FailEmoji { get; set; }
        public ulong? MinecraftRoleID { get; set; }
    }

    public class DiscordOptionsValidator : IValidateOptions<DiscordOptions> {
        public ValidateOptionsResult Validate(string name, DiscordOptions options) {
            if(string.IsNullOrWhiteSpace(options.ApiToken)) {
                return ValidateOptionsResult.Fail("No Discord API token was given");
            }
            return ValidateOptionsResult.Success;
        }
    }
}
