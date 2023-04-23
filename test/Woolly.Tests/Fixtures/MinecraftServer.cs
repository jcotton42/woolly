using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Woolly.Tests.Fixtures;

public sealed class MinecraftServer : IAsyncLifetime
{
    const int RconContainerPort = 25575;

    private readonly IContainer _container;

    public string Hostname { get; private set; } = null!;
    public string RconPassword { get; private set; }
    public int RconPort { get; private set; }

    public MinecraftServer()
    {
        RconPassword = Guid.NewGuid().ToString("N");
        _container = new ContainerBuilder()
            .WithImage("docker.io/itzg/minecraft-server:java8")
            .WithEnvironment("EULA", "TRUE")
            .WithEnvironment("VERSION", "1.12.2")
            .WithEnvironment("ENABLE_RCON", "true")
            .WithEnvironment("RCON_PORT", RconContainerPort.ToString())
            .WithEnvironment("RCON_PASSWORD", RconPassword)
            .WithPortBinding(RconContainerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilContainerIsHealthy())
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Hostname = _container.Hostname;
        RconPort = _container.GetMappedPublicPort(RconContainerPort);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
