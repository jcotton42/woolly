using MediatR.NotificationPublishers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;

using Npgsql;

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
            AddWoollyServices(builder, services);
            AddDiscordServices(services);
            services.AddMediatR(cfg =>
            {
                cfg
                    .RegisterServicesFromAssemblyContaining<Program>()
                    .AddOpenBehavior(typeof(RequestIdLoggingBehavior<,>))
                    .AddOpenBehavior(typeof(CommandNameLoggingBehavior<,>));
                cfg.NotificationPublisher = new ForeachAwaitPublisher();
            });
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

void AddWoollyServices(HostBuilderContext builder, IServiceCollection services)
{
    services.AddHostedService<Startup>();
    services.AddDbContext<WoollyContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString(nameof(WoollyContext))));
    services.AddScoped<WoollyRequestContext>();
    services.AddScoped<ServerListPingClientFactory>();
    services.AddScoped<ITcpPacketTransport, TcpPacketTransport>();
}

void AddDiscordServices(IServiceCollection services)
{
    services
        .AddValidatedOptions<DiscordOptions, DiscordOptions.Validator>(DiscordOptions.SectionName);

    services
        .AddDiscordService(services => services.GetRequiredService<IOptions<DiscordOptions>>().Value.Token)
        .Configure<DiscordGatewayClientOptions>(g => g.Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers);

    services
        .AddDiscordCommands(enableSlash: true)
        .AddRespondersFromAssembly(typeof(Program).Assembly)
        .AddCommandGroupsFromAssembly(typeof(Program).Assembly)
        .AddPostExecutionEvent<PostCommandExecutionHandler>()
        .AddAutocompleteProvider<MinecraftServerNameAutocompleter>();

    services
        .AddInteractivity()
        .AddInteractionGroupsFromAssembly(typeof(Program).Assembly);
}

// https://medium.com/@floyd.may/ef-core-app-migrate-on-startup-d046afdba258
// https://gist.github.com/Tim-Hodge/eea0601a14177c199fe60557eeeff31e
void Migrate<TContext>(IHost host) where TContext : DbContext
{
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

    if (diffsExist)
    {
        throw new InvalidOperationException(
            "There are differences between the current database model and the most recent migration.");
    }

    ctx.Database.Migrate();

    using var conn = (NpgsqlConnection)ctx.Database.GetDbConnection();
    conn.Open();
    conn.ReloadTypes();
}
