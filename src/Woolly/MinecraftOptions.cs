using System.Collections.Generic;

namespace Woolly {
    public class MinecraftOptions {
        public const string SectionName = "Minecraft";

        // = null! tells C# to consider the value non-null
        public Dictionary<string, MinecraftServer> Servers { get; set; }
    }

    public class MinecraftServer {
        public string Host { get; set; }
        public ushort RconPort { get; set; }
        public string RconPassword { get; set; }
        public ushort QueryPort { get; set; }
    }
}
