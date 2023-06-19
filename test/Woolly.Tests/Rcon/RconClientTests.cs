using FluentAssertions;

using Moq;

using Remora.Results;

using Woolly.Features.Rcon;
using Woolly.Infrastructure;
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
        _client = new RconClient(_fakeRconServer, new RconOptions { Hostname = Host, Port = Port, Password = Password });
    }

    [Fact]
    public async Task Login_With_Correct_Password_Does_Not_Throw()
    {
        await _client
            .Awaiting(c => c.ConnectAsync(CancellationToken.None))
            .Should()
            .NotThrowAsync();
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Throws()
    {
        var client = new RconClient(_fakeRconServer,
            new RconOptions { Hostname = Host, Port = Port, Password = "This ain't right" });
        var result = await client.ConnectAsync(CancellationToken.None);
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeOfType<ArgumentInvalidError>();
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
        var transport = new Mock<ITcpPacketTransport>();
        var client = new RconClient(transport.Object,
            new RconOptions { Hostname = "foo", Port = 123, Password = "bar" });
        transport.SetupSequence(c =>
                c.ReceiveAsync(It.IsAny<PacketReader<RconPacket>>(), It.IsAny<CancellationToken>()).Result)
            .Returns(new RconPacket
            {
                Id = client.NextId, Type = RconPacketType.Response, Payload = "Logged in",
            })
            .Returns(new RconPacket
            {
                Id = client.NextId + 1, Type = RconPacketType.Response, Payload = new string('a', maxPayloadSize),
            })
            .Returns(new RconPacket
            {
                Id = client.NextId + 1, Type = RconPacketType.Response, Payload = new string('a', maxPayloadSize),
            })
            .Returns(new RconPacket
            {
                Id = client.NextId + 1, Type = RconPacketType.Response, Payload = new string('a', maxPayloadSize),
            })
            .Returns(new RconPacket
            {
                Id = client.NextId + 1, Type = RconPacketType.Response, Payload = new string('a', 12),
            })
            .Returns(new RconPacket { Id = client.NextId + 2, Type = RconPacketType.Response, Payload = "", })
            .Throws(new InvalidOperationException("Called Receive too many times!"));

        await client.ConnectAsync(CancellationToken.None);
        var reply = await client.SendCommandAsync("test", CancellationToken.None);
        reply.IsSuccess.Should().BeTrue();
        reply.Entity.Should().HaveLength(3 * 4096 + 12);
    }
}