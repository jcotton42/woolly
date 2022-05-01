using Microsoft.Extensions.Options;

namespace Woolly; 

public class MinecraftOptions {
    public const string SectionName = "Minecraft";

    public Dictionary<string, MinecraftServer> Servers { get; set; } = null!;
}

public class MinecraftServer {
    public string Host { get; set; } = null!;
    public ushort RconPort { get; set; }
    public string RconPassword { get; set; } = null!;
    public ushort QueryPort { get; set; }
}

public class MinecraftOptionsValidator : IValidateOptions<MinecraftOptions> {
    public ValidateOptionsResult Validate(string name, MinecraftOptions options) {
        var failures = new List<string>();

        if(options.Servers is null || options.Servers.Count == 0) {
            failures.Add("No Minecraft servers defined");
            return ValidateOptionsResult.Fail(failures);
        }
        foreach(var (serverName, server) in options.Servers) {
            if(string.IsNullOrWhiteSpace(server.Host)) {
                failures.Add($"Minecraft server `{serverName}` has no host defined");
            }
            if(server.RconPort == 0) {
                failures.Add($"Minecraft server `{serverName}` has an invalid RCON port");
            }
            if(string.IsNullOrWhiteSpace(server.RconPassword)) {
                failures.Add($"Minecraft server `{serverName}` has no RCON password");
            }
            if(server.QueryPort == 0) {
                failures.Add($"Minecraft server `{serverName}` has an invalid query port");
            }
        }

        return failures.Any() ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
