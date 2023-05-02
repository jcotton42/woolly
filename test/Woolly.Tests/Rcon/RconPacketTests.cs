using System.Buffers;
using System.Buffers.Binary;

using FluentAssertions;

using Woolly.Features.Rcon;

namespace Woolly.Tests.Rcon;

public sealed class RconPacketTests
{
    [Theory]
    [MemberData(nameof(EncoderDecoderTestData))]
    public void EncoderTests(RconPacket packet, byte[] expected)
    {
        var encoded = new byte[expected.Length];
        var couldEncode = packet.TryWrite(encoded, out var encodedLength);
        couldEncode.Should().BeTrue();
        encodedLength.Should().Be(expected.Length);
        encoded.Should().Equal(expected);
    }

    [Theory]
    [MemberData(nameof(EncoderDecoderTestData))]
    public void DecoderTests(RconPacket expected, byte[] bytes)
    {
        var couldDecode = RconPacket.TryRead(new ReadOnlySequence<byte>(bytes), out var decoded, out _);
        couldDecode.Should().BeTrue();
        decoded.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Decoder_Bails_When_Buffer_Is_Too_Short_To_Contain_Length()
    {
        var buffer = new byte[2];
        RconPacket.TryRead(new ReadOnlySequence<byte>(buffer), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Decoder_Bails_When_Buffer_Is_Too_Short_To_Contain_Packet()
    {
        var buffer = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 500);
        RconPacket.TryRead(new ReadOnlySequence<byte>(buffer), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Encoder_Bails_When_Buffer_Is_Too_Short_To_Contain_Packet()
    {
        var buffer = new byte[8];
        var packet = new RconPacket
        {
            Id = 0x11223344,
            Type = RconPacketType.Login,
            Payload = "A very long string that will definitely not fit in that buffer. No way, no how.",
        };
        packet.TryWrite(buffer, out _).Should().BeFalse();
        buffer.Should().AllBeEquivalentTo(0, because: "if the buffer is too short, it should be unmodified");
    }

    public static TheoryData<RconPacket, byte[]> EncoderDecoderTestData { get; } = new()
    {
        // @formatter:off
        {
            new RconPacket
            {
                Id = 0x00000012,
                Type = RconPacketType.Login,
                Payload = "abcd"
            },
            new byte[]
            {
                0x0E, 0x00, 0x00, 0x00, // remainder length
                0x12, 0x00, 0x00, 0x00, // request ID
                0x03, 0x00, 0x00, 0x00, // type
                0x61, 0x62, 0x63, 0x64, 0x00, // payload + NULL
                0x00 // NULL padding
            }
        },
        {
            new RconPacket
            {
                Id = 0x1ABCDEF2,
                Type = RconPacketType.Command,
                Payload = "abcd\xA7q"
            },
            new byte[]
            {
                0x10, 0x00, 0x00, 0x00, // remainder length
                0xF2, 0xDE, 0xBC, 0x1A, // request ID
                0x02, 0x00, 0x00, 0x00, // type
                0x61, 0x62, 0x63, 0x64, 0xA7, 0x71, 0x00, // payload + NULL
                0x00 // NULL padding
            }
        },
        // @formatter:on
    };
}
