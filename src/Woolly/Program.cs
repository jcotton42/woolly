using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Woolly.Services;

namespace Woolly {
    public class Program {
        public static void Main(string[] args) {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureServices((hostContext, services) => {
                    services.Configure<DiscordOptions>(
                        hostContext.Configuration.GetSection(DiscordOptions.SectionName));
                    services.Configure<MinecraftOptions>(
                        hostContext.Configuration.GetSection(MinecraftOptions.SectionName));

                    services.AddSingleton<IMinecraftClientFactory, MinecraftClientFactory>();
                    services.AddHostedService<Worker>();
                });
    }
}
