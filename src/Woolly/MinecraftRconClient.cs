using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;

namespace Woolly;

public sealed class MinecraftRconClient : IDisposable
{
    private Socket _socket;
    private PipeReader _reader;
    private PipeWriter _writer;

    public MinecraftRconClient()
    {
        // TODO create socket
        var pipe = new Pipe();
        _reader = pipe.Reader;
        _writer = pipe.Writer;
    }

    private async Task FillPipeAsync(PipeWriter writer)
    {
        const int bufferSize = 4096;
        while (true)
        {
            var memory = writer.GetMemory(bufferSize);
            var read = await _socket.ReceiveAsync(memory, SocketFlags.None);
        }
    }

    public void Dispose()
    {
        throw new NotImplementedException();
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

    public int Encode(Span<byte> buffer)
    {
        var length = 4; // we'll write the length last
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(length), Id);
        length += 4;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(length), (int)Type);
        length += 4;
        length += Encoding.ASCII.GetBytes(Payload, buffer.Slice(length));
        buffer[length] = 0;
        buffer[length + 1] = 0;
        length += 2;
        BinaryPrimitives.WriteInt32LittleEndian(buffer, length - 4);
        return length;
    }

    private static Packet Decode(Span<byte> buffer)
    {
        var offset = 0;
        var id = BinaryPrimitives.ReadInt32LittleEndian(buffer);
        offset += 4;
        var type = (PacketType)BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset));
        offset += 4;
        var payload = Encoding.ASCII.GetString(buffer[offset..^2]); // ^2 for NULL terminator and NULL padding byte

        return new Packet(id, type, payload);
    }
}

public enum PacketType : int
{
    MultiResponse = 0,
    Command = 2,
    Login = 3,
}
