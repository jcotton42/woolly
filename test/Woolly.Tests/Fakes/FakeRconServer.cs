using System.Diagnostics;
using System.Threading.Channels;

using Remora.Results;

using Woolly.Features.Rcon;
using Woolly.Infrastructure;

namespace Woolly.Tests.Fakes;

public sealed class FakeRconServer : ITcpPacketTransport
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

    public async Task<Result<T>> ReceiveAsync<T>(PacketReader<T> tryReadPacket, CancellationToken ct) where T : notnull
    {
        AssertConnected();
        return Result<T>.FromSuccess((T)(object)await _channel.Reader.ReadAsync(ct));
    }

    public Task<Result> SendAsync<T>(T packet, PacketWriter<T> writePacket, CancellationToken ct) where T : notnull
    {
        AssertConnected();
        var rconPacket = (RconPacket)(object)packet;
        if (_isAuthenticated && rconPacket.Type == RconPacketType.Command)
        {
            if (rconPacket.Payload == "")
            {
                _channel.Writer.TryWrite(new RconPacket
                {
                    Id = rconPacket.Id, Type = RconPacketType.Response, Payload = "",
                });
            }
            else if (SendLongResponses)
            {
                var count = Random.Shared.Next(5000, 12000);
                var payload =
                    $"In response to Id = {rconPacket.Id}, Payload = {rconPacket.Payload}, here's {count} ones: {new string('1', count)}";
                foreach (var response in ChunkPayload(rconPacket.Id, payload))
                {
                    _channel.Writer.TryWrite(response);
                }
            }
            else
            {
                _channel.Writer.TryWrite(new RconPacket
                {
                    Id = rconPacket.Id,
                    Type = RconPacketType.Response,
                    Payload = $"Response to Id = {rconPacket.Id}, Payload = {rconPacket.Payload}",
                });
            }
        }
        else if (_isAuthenticated)
        {
            throw new InvalidOperationException($"Client is authenticated, but sent packet type of {rconPacket.Type}");
        }
        else if (rconPacket.Type == RconPacketType.Login && rconPacket.Payload == _password)
        {
            _isAuthenticated = true;
            _channel.Writer.TryWrite(new RconPacket
            {
                Id = rconPacket.Id, Type = RconPacketType.Login, Payload = "Logged in",
            });
        }
        else
        {
            _channel.Writer.TryWrite(new RconPacket
            {
                Id = -1, Type = RconPacketType.Login, Payload = "Wrong password or not logged in",
            });
        }

        return Task.FromResult(Result.FromSuccess());
    }

    private static IEnumerable<RconPacket> ChunkPayload(int id, string payload)
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

    public void AssertConnected()
    {
        if (!IsConnected) throw new InvalidOperationException("Not connected.");
    }

    public void Dispose() { }
}
