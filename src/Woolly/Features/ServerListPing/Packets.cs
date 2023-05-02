using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Woolly.Features.ServerListPing;

public interface IOutbound
{
    /// <summary>
    /// The packet's ID.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Gets the length of the packet's data.
    /// </summary>
    /// <remarks>
    /// Does not include the ID.
    /// </remarks>
    int GetDataLength();

    /// <summary>
    /// Writes the packet's data to the given buffer.
    /// </summary>
    void WriteData(Span<byte> buffer);
}

public interface IInbound<T> where T : IInbound<T>
{
    int Id { get; }
    static abstract bool TryReadData(ref ReadOnlySequence<byte> buffer, int id, out T packet);
}

public readonly struct Handshake : IOutbound
{
    public int Id => 0;
    public required int ProtocolVersion { get; init; }
    public required string ServerAddress { get; init; }
    public required ushort ServerPort { get; init; }
    public int NextState => 1; // 1 = Status

    public int GetDataLength() =>
        VarInt.GetByteCount(ProtocolVersion)
        + VarString.GetByteCount(ServerAddress)
        + sizeof(ushort)
        + VarInt.GetByteCount(NextState);

    public void WriteData(Span<byte> buffer)
    {
        var offset = VarInt.Write(buffer, ProtocolVersion);
        offset += VarString.Write(buffer[offset..], ServerAddress);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[offset..], ServerPort);
        offset += sizeof(ushort);
        VarInt.Write(buffer[offset..], NextState);
    }
}

public readonly struct StatusRequest : IOutbound
{
    public int Id => 0;
    public int GetDataLength() => 0;
    public void WriteData(Span<byte> buffer) { }
}

public readonly struct StatusResponse : IInbound<StatusResponse>
{
    public required int Id { get; init; }
    public required ServerStatus Status { get; init; }

    public static bool TryReadData(ref ReadOnlySequence<byte> buffer, int id, out StatusResponse packet)
    {
        packet = default;
        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryReadVarJson(out ServerStatus? status)) return false;
        packet = new StatusResponse { Id = id, Status = status };
        buffer = buffer.Slice(reader.Position);
        return true;
    }
}

public readonly struct Ping : IInbound<Ping>, IOutbound
{
    public int Id => 1;
    public required long Payload { get; init; }

    public int GetDataLength() => sizeof(long);
    public void WriteData(Span<byte> buffer) => BinaryPrimitives.WriteInt64BigEndian(buffer, Payload);

    public static bool TryReadData(ref ReadOnlySequence<byte> buffer, int id, out Ping packet)
    {
        packet = default;
        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryReadBigEndian(out long payload)) return false;
        packet = new Ping { Payload = payload };
        buffer = buffer.Slice(reader.Position);
        return true;
    }
}

public sealed class ServerStatus
{
    [JsonPropertyName("version")]
    public required VersionPayload Version { get; init; }

    [JsonPropertyName("players")]
    public required PlayersPayload Players { get; init; }

    [JsonPropertyName("description")]
    public Description? Description { get; init; }

    [JsonPropertyName("favicon")]
    public string? Favicon { get; init; }
}

public sealed class Description
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed class PlayersPayload
{
    [JsonPropertyName("max")]
    public int Max { get; init; }

    [JsonPropertyName("online")]
    public int Online { get; init; }

    [JsonPropertyName("sample")]
    public Player[]? Players { get; init; }
}

public sealed class Player
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

public sealed class VersionPayload
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("protocol")]
    public required int Protocol { get; init; }
}

public static class VarString
{
    public static bool TryReadVarString(ref this SequenceReader<byte> reader, [NotNullWhen(true)] out string? value)
    {
        value = default;
        if (!reader.TryReadVarInt(out var bytes)) return false;
        if (!reader.TryReadExact(bytes, out var sequence)) return false;
        value = Encoding.UTF8.GetString(sequence);
        return true;
    }

    public static int Write(Span<byte> buffer, string value)
    {
        var length = Encoding.UTF8.GetByteCount(value);
        var offset = VarInt.Write(buffer, length);
        offset += Encoding.UTF8.GetBytes(value, buffer[offset..]);
        return offset;
    }

    public static int GetByteCount(string value)
    {
        var length = Encoding.UTF8.GetByteCount(value);
        return VarInt.GetByteCount(length) + length;
    }
}

public static class VarJson
{
    public static bool TryReadVarJson<T>(ref this SequenceReader<byte> reader, [NotNullWhen(true)] out T? value)
    {
        value = default;
        if (!reader.TryReadVarInt(out var byteCount)) return false;
        if (!reader.TryReadExact(byteCount, out var sequence)) return false;
        var jsonReader = new Utf8JsonReader(sequence);
        value = JsonSerializer.Deserialize<T>(ref jsonReader)!;
        return true;
    }
}

public static class VarInt
{
    private const int SegmentBits = 0x7F;
    private const int ContinueBit = 0x80;

    public static int GetByteCount(int value) => value switch
    {
        < 0 => 5,
        < 1 << 7 => 1,
        < 1 << 14 => 2,
        < 1 << 21 => 3,
        < 1 << 28 => 4,
        _ => 5,
    };

    public static bool TryReadVarInt(ref this SequenceReader<byte> reader, out int value)
    {
        value = 0;
        var position = 0;

        while (true)
        {
            if (!reader.TryRead(out var currentByte)) return false;
            value |= (currentByte & SegmentBits) << position;

            if ((currentByte & ContinueBit) == 0) break;

            position += 7;

            if (position >= 32) throw new InvalidOperationException("VarInt is too big");
        }

        return true;
    }

    public static int Write(Span<byte> buffer, int value)
    {
        int length = 0;
        while (true)
        {
            if ((value & ~SegmentBits) == 0)
            {
                buffer[length] = (byte)value;
                length++;
                return length;
            }

            buffer[length] = (byte)((value & SegmentBits) | ContinueBit);

            value >>>= 7;
            length++;
        }
    }
}
