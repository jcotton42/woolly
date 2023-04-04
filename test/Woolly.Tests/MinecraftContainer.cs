using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Woolly.Tests;

public sealed class MinecraftContainer : IAsyncLifetime
{
    private const int RconPortNumber = 25575;
    private readonly IContainer _container;

    public string Hostname => _container.Hostname;
    public string RconPassword { get; }
    public int RconPort => _container.GetMappedPublicPort(RconPortNumber);

    public MinecraftContainer()
    {
        RconPassword = Guid.NewGuid().ToString();
        _container = new ContainerBuilder()
            .WithImage("docker.io/itzg/minecraft-server:java8")
            .WithEnvironment("VERSION", "1.12.2")
            .WithEnvironment("EULA", "TRUE")
            .WithEnvironment("ENABLE_RCON", "true")
            .WithEnvironment("RCON_PASSWORD", RconPassword)
            .WithPortBinding(RconPortNumber, assignRandomHostPort: true)
            .Build();
    }

    public async Task RestartAsync()
    {
        await _container.StopAsync();
        await _container.StartAsync();
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _container.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
