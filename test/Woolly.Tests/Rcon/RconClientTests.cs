using FluentAssertions;

using Moq;

using Woolly.Rcon;
using Woolly.Tests.Fakes;

namespace Woolly.Tests.Rcon;

public sealed class RconClientTests
{
    private const string Host = "foo";
    private const int Port = 25575;
    private const string Password = "foofoo";

    private readonly FakeRconServer _fakeRconServer;
    private readonly RconClient _client;

    public RconClientTests()
    {
        _fakeRconServer = new FakeRconServer(Host, Port, Password);
        _client = new RconClient(_fakeRconServer);
    }

    [Fact]
    public async Task Login_With_Correct_Password_Does_Not_Throw()
    {
        await _client
            .Awaiting(c => c.ConnectAsync(Host, Port, Password, CancellationToken.None))
            .Should()
            .NotThrowAsync();
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Throws()
    {
        await _client
            .Awaiting(c => c.ConnectAsync(Host, Port, "this ain't right", CancellationToken.None))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Sending_Before_Connecting_Throws()
    {
        await _client
            .Awaiting(c => c.SendCommandAsync("foobar", CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Send_Command_Joins_Long_Replies()
    {
        const int maxPayloadSize = 4096;
        var connection = new Mock<IRconConnection>();
        var client = new RconClient(connection.Object);
        connection.SetupSequence(c => c.ReceiveAsync(It.IsAny<CancellationToken>()).Result)
            .Returns(new RconPacket
            {
                Id = client.NextId, Type = RconPacketType.Response, Payload = new string('a', maxPayloadSize),
            })
            .Returns(new RconPacket
            {
                Id = client.NextId, Type = RconPacketType.Response, Payload = new string('a', maxPayloadSize),
            })
            .Returns(new RconPacket
            {
                Id = client.NextId, Type = RconPacketType.Response, Payload = new string('a', maxPayloadSize),
            })
            .Returns(new RconPacket
            {
                Id = client.NextId, Type = RconPacketType.Response, Payload = new string('a', 12),
            })
            .Returns(new RconPacket { Id = client.NextId + 1, Type = RconPacketType.Response, Payload = "", })
            .Throws(new InvalidOperationException("Called Receive too many times!"));
        connection.Setup(c => c.IsConnected).Returns(true);

        var reply = await client.SendCommandAsync("test", CancellationToken.None);
        reply.Should().HaveLength(3 * 4096 + 12);
    }
}
