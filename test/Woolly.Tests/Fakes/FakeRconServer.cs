using System.Threading.Channels;

using Woolly.Rcon;

namespace Woolly.Tests.Fakes;

public sealed class FakeRconServer : IRconConnection
{
    private readonly Channel<RconPacket> _channel;
    private readonly string _host;
    private readonly int _port;
    private readonly string _password;

    private bool _isAuthenticated;

    public FakeRconServer(string host, int port, string password)
    {
        _channel = Channel.CreateUnbounded<RconPacket>();
        _host = host;
        _port = port;
        _password = password;
    }

    public bool IsConnected { get; private set; }
    public bool SendLongResponses { get; set; }

    public Task ConnectAsync(string host, int port, CancellationToken token)
    {
        if (host != _host || port != _port) throw new ArgumentException("Incorrect host and port.");
        IsConnected = true;
        return Task.CompletedTask;
    }

    public async Task<RconPacket?> ReceiveAsync(CancellationToken token)
    {
        AssertConnected();
        return await _channel.Reader.ReadAsync(token);
    }

    public Task SendAsync(RconPacket packet, CancellationToken token)
    {
        AssertConnected();
        if (_isAuthenticated && packet.Type == RconPacketType.Command)
        {
            if (packet.Payload == "")
            {
                _channel.Writer.TryWrite(new RconPacket
                {
                    Id = packet.Id, Type = RconPacketType.Response, Payload = "",
                });
            }
            else if (SendLongResponses)
            {
                var count = Random.Shared.Next(5000, 12000);
                var payload =
                    $"In response to Id = {packet.Id}, Payload = {packet.Payload}, here's {count} ones: {new string('1', count)}";
                foreach (var response in ChunkPayload(packet.Id, payload))
                {
                    _channel.Writer.TryWrite(response);
                }
            }
            else
            {
                _channel.Writer.TryWrite(new RconPacket
                {
                    Id = packet.Id,
                    Type = RconPacketType.Response,
                    Payload = $"Response to Id = {packet.Id}, Payload = {packet.Payload}",
                });
            }
        }
        else if (_isAuthenticated)
        {
            throw new InvalidOperationException($"Client is authenticated, but sent packet type of {packet.Type}");
        }
        else if (packet.Type == RconPacketType.Login && packet.Payload == _password)
        {
            _isAuthenticated = true;
            _channel.Writer.TryWrite(new RconPacket
            {
                Id = packet.Id, Type = RconPacketType.Login, Payload = "Logged in",
            });
        }
        else
        {
            _channel.Writer.TryWrite(new RconPacket
            {
                Id = -1, Type = RconPacketType.Login, Payload = "Wrong password or not logged in",
            });
        }

        return Task.CompletedTask;
    }

    private IEnumerable<RconPacket> ChunkPayload(int id, string payload)
    {
        const int maxPayloadSize = 4096;
        for (int start = 0; start < payload.Length; start += maxPayloadSize)
        {
            yield return new RconPacket
            {
                Id = id,
                Type = RconPacketType.Response,
                Payload = payload.Substring(start, Math.Min(maxPayloadSize, payload.Length - start)),
            };
        }
    }

    private void AssertConnected()
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected.");
    }

    public void Dispose() { }
}
