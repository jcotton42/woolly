using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.EntityFrameworkCore;

using Remora.Rest.Core;
using Remora.Results;

using Woolly.Data;
using Woolly.Infrastructure;

namespace Woolly.Features.ServerListPing;

public sealed class ServerListPingClientFactory
{
    private readonly WoollyContext _db;
    private readonly IServiceProvider _serviceProvider;

    public ServerListPingClientFactory(WoollyContext db, IServiceProvider serviceProvider)
    {
        _db = db;
        _serviceProvider = serviceProvider;
    }

    public async Task<Result<ServerListPingClient>> GetClientAsync(Snowflake guildId, string name, CancellationToken ct)
    {
        var server = await _db.MinecraftServers.FirstOrDefaultAsync(s => s.GuildId == guildId && s.Name == name, ct);
        if (server is null) return new NotFoundError($"No Minecraft server named `{name}` is registered in this guild.");

        var options = new ServerListPingClientOptions { Host = server.Host, Port = server.PingPort };
        var client = ActivatorUtilities.CreateInstance<ServerListPingClient>(_serviceProvider, options);

        var connectResult = await client.ConnectAsync(ct);
        if (!connectResult.IsSuccess) return Result<ServerListPingClient>.FromError(connectResult);

        return client;
    }
}

public sealed partial class ServerListPingClient
{
    // TODO find out what sending -1 does
    // TODO also test with very low and very high (lower/higher than the Minecraft version supports) versions
    private const int ProtocolVersion = -1;

    private readonly ILogger _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly ITcpPacketTransport _transport;

    public ServerListPingClient(ILogger<ServerListPingClient> logger,
        ITcpPacketTransport transport, ServerListPingClientOptions options)
    {
        _logger = logger;
        _host = options.Host;
        _port = options.Port;
        _transport = transport;
    }

    public async Task<Result> ConnectAsync(CancellationToken ct)
    {
        await _transport.ConnectAsync(_host, _port, ct);
        var handshake = new Handshake
        {
            ProtocolVersion = ProtocolVersion, ServerAddress = _host, ServerPort = (ushort)_port,
        };

        var result = await _transport.SendAsync(handshake, WritePacket, ct);
        if (result.IsSuccess) Connected(_host, _port);
        else ConnectFailed(_host, _port, result.Error.Message);
        return result;
    }

    public async Task<Result<ServerStatus>> GetStatusAsync(CancellationToken ct)
    {
        _transport.AssertConnected();

        var sendResult = await _transport.SendAsync(new StatusRequest(), WritePacket, ct);
        if (!sendResult.IsSuccess)
        {
            StatusFailed(_host, _port, sendResult.Error.Message);
            return Result<ServerStatus>.FromError(sendResult);
        }

        var receiveResult = await _transport.ReceiveAsync<StatusResponse>(TryReadPacket, ct);
        if (!receiveResult.IsSuccess)
        {
            StatusFailed(_host, _port, receiveResult.Error.Message);
            return Result<ServerStatus>.FromError(receiveResult);
        }

        return receiveResult.Entity.Status;
    }

    public async Task<Result<TimeSpan>> PingAsync(CancellationToken ct)
    {
        _transport.AssertConnected();

        var sw = Stopwatch.StartNew();

        var sendResult = await _transport.SendAsync(new Ping { Payload = Random.Shared.NextInt64() }, WritePacket, ct);
        if (!sendResult.IsSuccess)
        {
            PingFailed(_host, _port, sendResult.Error.Message);
            return Result<TimeSpan>.FromError(sendResult);
        }

        var receiveResult = await _transport.ReceiveAsync<Ping>(TryReadPacket, ct);
        if (!receiveResult.IsSuccess)
        {
            PingFailed(_host, _port, receiveResult.Error.Message);
            return Result<TimeSpan>.FromError(receiveResult);
        }

        return sw.Elapsed;
    }

    private static bool TryReadPacket<T>(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out T? packet) where T : IInbound<T>
    {
        packet = default;
        var reader = new SequenceReader<byte>(buffer);
        if (!reader.TryReadVarInt(out var length)) return false;
        if (!reader.TryReadVarInt(out var id)) return false;
        if (!reader.TryReadExact(length - VarInt.GetByteCount(id), out var data)) return false;

        if (!T.TryReadData(ref data, id, out packet)) return false;
        buffer = data;
        return true;
    }

    private static void WritePacket<T>(IBufferWriter<byte> writer, T packet) where T : IOutbound
    {
        // packet format
        // Length       VarInt (Length of Packet ID + Data)
        // Packet ID    VarInt
        // Data         Varies
        var idLength = VarInt.GetByteCount(packet.Id);
        var dataLength = packet.GetDataLength();
        var length = idLength + dataLength;
        var packetLength = VarInt.GetByteCount(length) + length;
        var buffer = writer.GetSpan(packetLength);

        var offset = VarInt.Write(buffer, length);
        offset += VarInt.Write(buffer[offset..], packet.Id);
        packet.WriteData(buffer[offset..]);
        writer.Advance(packetLength);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Connected to {Host}:{Port}")]
    private partial void Connected(string host, int port);

    [LoggerMessage(EventId = 100, Level = LogLevel.Error, Message = "Failed to connect to {Host}:{Port}: {Message}")]
    private partial void ConnectFailed(string host, int port, string message);

    [LoggerMessage(EventId = 101, Level = LogLevel.Error,
        Message = "Failed to get status for {Host}:{Port}: {Message}")]
    private partial void StatusFailed(string host, int port, string message);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "Ping failed for {Host}:{Port}: {Message}")]
    private partial void PingFailed(string host, int port, string message);
}

public sealed class ServerListPingClientOptions
{
    public required string Host { get; set; }
    public required int Port { get; set; }
}
