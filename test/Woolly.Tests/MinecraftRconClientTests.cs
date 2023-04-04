using DotNet.Testcontainers.Configurations;

using FluentAssertions;

namespace Woolly.Tests;

public sealed class MinecraftRconClientTests : IClassFixture<MinecraftContainer>
{
    private readonly MinecraftContainer _container;

    public MinecraftRconClientTests(MinecraftContainer container) => _container = container;

    [Fact]
    public void Incorrect_Password_Returns_False()
    {
        var result = MinecraftRconClient.TryCreate(_container.Hostname, _container.RconPort, RandomString.Create(10),
            out var client);
        result.Should().BeFalse();
        client.Should().BeNull();
    }
}
