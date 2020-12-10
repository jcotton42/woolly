using System.Collections.Generic;

namespace Woolly {
    public class DiscordOptions {
        public const string SectionName = "Discord";

        public string ApiToken { get; set; }
        public IDictionary<string, GuildOptions> GuildOptions { get; set; }
    }

    public class GuildOptions {
        public string OkEmoji { get; set; }
        public string FailEmoji { get; set; }
    }
}
