using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace Woolly {
    public class DiscordOptions {
        // null! tells the compiler to assume the field isn't null
        // validation will take care of that for us
        public const string SectionName = "Discord";

        public string ApiToken { get; set; } = null!;
        public string[]? CommandPrefixes { get; set; }
        public Dictionary<string, GuildOptions> GuildOptions { get; set; } = new();
    }

    public class GuildOptions {
        public string OkEmoji { get; set; } = ":ballot_box_with_check:";
        public string FailEmoji { get; set; } = ":x:";
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
