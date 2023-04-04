using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;

namespace Woolly;

public sealed class MinecraftRconClient : IDisposable
{
    const int BufferSize = 4096;

    private readonly byte[] _buffer;
    private readonly NetworkStream _stream;

    private MinecraftRconClient(NetworkStream stream)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        _stream = stream;
    }

    public static bool TryCreate(string host, int port, string password, [NotNullWhen(true)] out MinecraftRconClient? client)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(host, port);
        var stream = new NetworkStream(socket, ownsSocket: true);
        var loginPacket = new Packet(Random.Shared.Next(), PacketType.Login, password);

        client = new MinecraftRconClient(stream);
        if(client.TrySendPacket(loginPacket, out _))
        {
            return true;
        }
        client.Dispose();
        client = null;
        return false;
    }

    public bool TryCommand(string command, out Packet response)
    {
        var packet = new Packet(Random.Shared.Next(), PacketType.Command, command);
        return TrySendPacket(packet, out response);
    }

    private bool TrySendPacket(Packet packet, out Packet response)
    {
        var length = EncodePacket(packet, _buffer);
        _stream.Write(_buffer, 0, length);
        var read = _stream.Read(_buffer);
        var responseEnd = BinaryPrimitives.ReadInt32LittleEndian(_buffer) + 4;
        response = DecodePacket(_buffer.AsSpan()[4..responseEnd]);

        return packet.Id == response.Id;
    }

    private static int EncodePacket(Packet packet, Span<byte> buffer)
    {
        var length = 4; // we'll write the length last
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(length), packet.Id);
        length += 4;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(length), (int)packet.Type);
        length += 4;
        length += Encoding.ASCII.GetBytes(packet.Payload, buffer.Slice(length));
        buffer[length] = 0;
        buffer[length + 1] = 0;
        length += 2;
        BinaryPrimitives.WriteInt32LittleEndian(buffer, length - 4);
        return length;
    }

    private static Packet DecodePacket(Span<byte> buffer)
    {
        var offset = 0;
        var id = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        offset += 4;
        var type = (PacketType)BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset));
        offset += 4;
        var payload = Encoding.ASCII.GetString(buffer[offset..^2]); // ^2 for NULL terminator and NULL padding byte

        return new Packet(id, type, payload);
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer, clearArray: true);
        _stream.Dispose();
    }
}

public readonly struct Packet
{
    public readonly int Id;
    public readonly PacketType Type;
    public readonly string Payload;

    public Packet(int id, PacketType type, string payload)
    {
        Id = id;
        Type = type;
        Payload = payload;
    }
}

public enum PacketType : int
{
    MultiResponse = 0,
    Command = 2,
    Login = 3,
}
