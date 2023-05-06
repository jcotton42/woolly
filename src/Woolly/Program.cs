using FluentValidation;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;

using Remora.Discord.API.Abstractions.Gateway.Commands;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Extensions.Extensions;
using Remora.Discord.Gateway;
using Remora.Discord.Hosting.Extensions;
using Remora.Discord.Interactivity.Extensions;

using Serilog;

using Woolly;
using Woolly.Data;
using Woolly.Extensions;
using Woolly.Features.ServerListPing;
using Woolly.Infrastructure;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((builder, services) =>
        {
            services.AddStartup();
            services.AddDbContext<WoollyContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(WoollyContext))));
            services.AddScoped<WoollyRequestContext>();
            services.AddScoped<ServerListPingClientFactory>();
            services.AddScoped<ITcpPacketTransport, TcpPacketTransport>();
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>()
                .AddOpenBehavior(typeof(RequestIdLoggingBehavior<,>))
                .AddOpenBehavior(typeof(CommandNameLoggingBehavior<,>)));
            AddDiscordServices(services);
        })
        .UseSerilog((context, loggerConfig) => loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", "woolly")
        )
        .Build();

    Migrate<WoollyContext>(host);
    host.Run();
    return 0;
}
catch (Exception e)
{
    Log.Fatal(e, "Host terminated unexpectedly.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

void AddDiscordServices(IServiceCollection services)
{
    services
        .AddSingleton<IValidateOptions<DiscordOptions>, DiscordOptions.Validator>()
        .AddOptions<DiscordOptions>()
        .BindConfiguration(DiscordOptions.SectionName)
        .ValidateOnStart();

    services
        .AddDiscordService(services => services.GetRequiredService<IOptions<DiscordOptions>>().Value.Token)
        .Configure<DiscordGatewayClientOptions>(g =>
            g.Intents = GatewayIntents.Guilds | GatewayIntents.GuildBans | GatewayIntents.GuildMembers)
        .AddDiscordCommands(enableSlash: true)
        .AddRespondersFromAssembly(typeof(Program).Assembly)
        .AddCommandGroupsFromAssembly(typeof(Program).Assembly)
        .AddPostExecutionEvent<PostCommandExecutionHandler>()
        .AddInteractivity()
        .AddInteractionGroupsFromAssembly(typeof(Program).Assembly)
        .AddAutocompleteProvider<MinecraftServerNameAutocompleter>();
}

// https://medium.com/@floyd.may/ef-core-app-migrate-on-startup-d046afdba258
// https://gist.github.com/Tim-Hodge/eea0601a14177c199fe60557eeeff31e
static void Migrate<TContext>(IHost host) where TContext : DbContext {
    using var scope = host.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
    using var ctx = scope.ServiceProvider.GetRequiredService<TContext>();

    var sp = ctx.GetInfrastructure();

    var modelDiffer = sp.GetRequiredService<IMigrationsModelDiffer>();
    var migrationsAssembly = sp.GetRequiredService<IMigrationsAssembly>();

    var modelInitializer = sp.GetRequiredService<IModelRuntimeInitializer>();
    var sourceModel = modelInitializer.Initialize(migrationsAssembly.ModelSnapshot!.Model);

    var designTimeModel = sp.GetRequiredService<IDesignTimeModel>();
    var readOptimizedModel = designTimeModel.Model;

    var diffsExist = modelDiffer.HasDifferences(
        sourceModel.GetRelationalModel(),
        readOptimizedModel.GetRelationalModel());

    if(diffsExist) {
        throw new InvalidOperationException("There are differences between the current database model and the most recent migration.");
    }

    ctx.Database.Migrate();
}
