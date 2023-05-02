using System.Text;

namespace Woolly.Features.Rcon;

public sealed class RconClient : IDisposable
{
    private readonly IRconConnection _connection;

    internal int NextId;

    public bool IsConnected => _connection.IsConnected;

    public RconClient(IRconConnection connection)
    {
        _connection = connection;
        NextId = 1;
    }

    public async Task ConnectAsync(string host, int port, string password, CancellationToken token)
    {
        await _connection.ConnectAsync(host, port, token);
        var loginPacket = new RconPacket { Id = NextId++, Type = RconPacketType.Login, Payload = password, };
        await _connection.SendAsync(loginPacket, token);
        var loginReply = await _connection.ReceiveAsync(token);
        if (loginReply is null)
        {
            // TODO retry
        }

        if (loginPacket.Id != loginReply.Value.Id) throw new ArgumentException("Invalid password.", nameof(password));
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken token)
    {
        AssertConnected();
        // Responses from an RCON server can be fragmented across multiple packets, but there's no standard end of
        // response flag. So send an "end" packet with a different ID, which will let us know when we've finished the
        // reply.
        var commandPacketId = NextId++;
        var endPacketId = NextId++;
        var commandPacket = new RconPacket { Id = commandPacketId, Type = RconPacketType.Command, Payload = command };
        var endPacket = new RconPacket { Id = endPacketId, Type = RconPacketType.Command, Payload = "" };

        await _connection.SendAsync(commandPacket, token);
        await _connection.SendAsync(endPacket, token);
        var result = new StringBuilder();

        while (await _connection.ReceiveAsync(token) is { } packet)
        {
            if (packet.Id == -1)
            {
                // TODO, pick a better exception type
                throw new NotImplementedException();
            }
            else if (packet.Id == commandPacketId)
            {
                result.Append(packet.Payload);
            }
            else
            {
                break;
            }
        }

        return result.ToString();
    }

    private void AssertConnected()
    {
        if (!_connection.IsConnected) throw new InvalidOperationException("Client is not connected.");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
