namespace Woolly.Tests;

public sealed class MinecraftRconClientTests : IClassFixture<MinecraftContainer>
{
    private readonly MinecraftContainer _container;

    public MinecraftRconClientTests(MinecraftContainer container) => _container = container;

    [Fact]
    public void Incorrect_Password_Returns_False()
    {

    }
}
