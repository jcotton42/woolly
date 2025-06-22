using Microsoft.EntityFrameworkCore;

using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.Commands.Services;
using Remora.Discord.Gateway;
using Remora.Discord.Gateway.Extensions;
using Remora.Discord.Hosting.Extensions;

using Serilog;

using Woolly;


Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

    var seqEndpoint = builder.Configuration["SeqEndpoint"];
    if (seqEndpoint is null && builder.Environment.IsDevelopment())
    {
        Log.Warning("Seq endpoint missing from config");
    }
    else if (seqEndpoint is null)
    {
        Log.Fatal("Seq endpoint missing from config, aborting");
        return 1;
    }

    builder.Services.AddSerilog((services, lc) =>
    {
        lc
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console();
        if (seqEndpoint is not null)
        {
            lc.WriteTo.Seq(seqEndpoint);
        }
    });

    var discordToken = builder.Configuration["Discord:Token"];
    if (discordToken is null)
    {
        Log.Fatal("Discord token missing from config, aborting");
        return 1;
    }

    var discordTestServerIdString = builder.Configuration["Discord:TestServerId"];
    if (discordTestServerIdString is null || !DiscordSnowflake.TryParse(discordTestServerIdString, out var testServer) && builder.Environment.IsDevelopment())
    {
        Log.Fatal("Test server ID is required during development, aborting");
        return 1;
    }

    AddAppServices(builder, discordToken);

    var host = builder.Build();

    // var slashService = host.Services.GetRequiredService<SlashService>();
    // var updateSlash = await slashService.UpdateSlashCommandsAsync(testServer);
    // if (!updateSlash.IsSuccess)
    // {
    //     Log.Warning("Failed to update slash commands: {Reason}", updateSlash.Error.Message);
    // }

    await host.RunAsync();
    return 0;
}
catch (Exception e)
{
    Log.Fatal(e, "Host terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

void AddAppServices(HostApplicationBuilder builder, string discordToken)
{
    builder.Services.AddDiscordService(_ => discordToken);
    builder.Services.Configure<DiscordGatewayClientOptions>(o => o.Intents = GatewayIntents.Guilds | GatewayIntents.GuildBans | GatewayIntents.GuildMembers);
    builder.Services.AddWoollyHandlers();

    // should be generated
    builder.Services.AddResponder<RegisterGuildCommand.Handler>();

    builder.Services.AddDbContext<WoollyContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
}
