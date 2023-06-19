using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Woolly.Features.Rcon;

public readonly struct RconPacket
{
    public required int Id { get; init; }
    public required RconPacketType Type { get; init; }
    public required string Payload { get; init; }

    /// <summary>
    /// Tries to read an <see cref="RconPacket"/> from the given byte sequence.
    /// </summary>
    /// <param name="buffer">
    /// The <see cref="ReadOnlySequence{T}"/> to read from. Advanced past the packet if read
    /// successfully.
    /// </param>
    /// <param name="packet">When <c>true</c> is returned, the read packet.</param>
    /// <returns>If the packet was successfully read.</returns>
    public static bool TryRead(ref ReadOnlySequence<byte> buffer, out RconPacket packet)
    {
        packet = default;
        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryReadLittleEndian(out int remaining)) return false;
        if (reader.Remaining < remaining) return false;
        // we now know the sequence has the whole packet, so no need to check these Trys
        _ = reader.TryReadLittleEndian(out int id);
        _ = reader.TryReadLittleEndian(out int type);
        // from https://wiki.vg/RCON
        // Note on ASCII text: Some servers reply with color codes prefixed by a section sign in their replies
        // (for example Craftbukkit for Minecraft 1.4.7).
        // The section sign is sent by those servers as byte 0xA7 or 167. This is not part of the US-ASCII charset and
        // will cause errors for clients that strictly use the US-ASCII charset. Using the ISO-LATIN-1/ISO-8859_1
        // charset instead of the US-ASCII charset yields much better results for those servers. Alternatively removing
        // byte 167 and one subsequent byte from the payload will remove all color tokens making the text more human
        // readable for clients that do not subsequently colorize those tokens.

        // 10 is from sizeof(id) + sizeof(type) + payload NULL terminator + NULL pad byte
        reader.TryReadExact(remaining - 10, out var payloadSequence);
        var payload = Encoding.Latin1.GetString(payloadSequence);
        // advance past NULL terminator and NULL pad byte
        reader.Advance(2);

        packet = new RconPacket { Id = id, Type = (RconPacketType)type, Payload = payload };
        buffer = buffer.Slice(reader.Position);
        return true;
    }

    /// <summary>
    /// Writes an <see cref="RconPacket"/> to the given <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">The buffer to write to.</param>
    public void Write(IBufferWriter<byte> writer)
    {
        const int idOffset = 4;
        const int typeOffset = 8;
        const int payloadOffset = 12;

        var payloadLength = Encoding.Latin1.GetByteCount(Payload);
        // 10 = sizeof(id) + sizeof(type) + NULL terminator on Payload + NULL pad byte
        var remainder = payloadLength + 10;
        // 4 bytes for the remainder
        var length = remainder + 4;
        var buffer = writer.GetSpan(length);
        BinaryPrimitives.WriteInt32LittleEndian(buffer, remainder);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[idOffset..], Id);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[typeOffset..], (int)Type);
        Encoding.Latin1.GetBytes(Payload, buffer[payloadOffset..]);
        buffer[payloadOffset + payloadLength] = 0; // NULL terminator
        buffer[payloadOffset + payloadLength + 1] = 0; // NULL padding byte
        writer.Advance(length);
    }
}

public enum RconPacketType : int
{
    Response = 0,
    Command = 2,
    Login = 3,
}