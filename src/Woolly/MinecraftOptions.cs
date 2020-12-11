namespace Woolly {
    public class MinecraftOptions {
        public const string SectionName = "Minecraft";

        public string Nickname { get; set; }
        public string IPAddress { get; set; }
        public ushort Port { get; set; }
        public string RconPassword { get; set; }
    }
}
