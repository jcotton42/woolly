using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;

using Microsoft.Extensions.Options;

using Remora.Discord.Commands.Extensions;
using Remora.Results;

namespace Woolly.ServerListPing;

public sealed partial class ServerListPingClient
{
    // TODO find out what sending -1 does
    private const int ProtocolVersion = -1;

    private readonly ILogger _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly ServerListPingTransport _transport;

    public ServerListPingClient(ILogger<ServerListPingClient> logger,
        ServerListPingTransport transport,
        IOptions<ServerListPingClientOptions> options)
    {
        _logger = logger;
        _host = options.Value.Host;
        _port = options.Value.Port;
        _transport = transport;
    }

    public async Task<Result> ConnectAsync(CancellationToken ct)
    {
        await _transport.ConnectAsync(_host, _port, ct);
        var handshake = new Handshake
        {
            ProtocolVersion = ProtocolVersion, ServerAddress = _host, ServerPort = (ushort)_port,
        };

        var result = await _transport.SendAsync(handshake, ct);
        if (result.IsSuccess) Connected(_host, _port);
        else ConnectFailed(_host, _port, result.Error.Message);
        return result;
    }

    public async Task<Result<ServerStatus>> GetStatusAsync(CancellationToken ct)
    {
        _transport.AssertConnected();

        var sendResult = await _transport.SendAsync(new StatusRequest(), ct);
        if (!sendResult.IsSuccess)
        {
            StatusFailed(_host, _port, sendResult.Error.Message);
            return Result<ServerStatus>.FromError(sendResult);
        }

        var receiveResult = await _transport.ReceiveAsync<StatusResponse>(ct);
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

        var sendResult = await _transport.SendAsync(new Ping { Payload = Random.Shared.NextInt64() }, ct);
        if (!sendResult.IsSuccess)
        {
            PingFailed(_host, _port, sendResult.Error.Message);
            return Result<TimeSpan>.FromError(sendResult);
        }

        var receiveResult = await _transport.ReceiveAsync<Ping>(ct);
        if (!receiveResult.IsSuccess)
        {
            PingFailed(_host, _port, receiveResult.Error.Message);
            return Result<TimeSpan>.FromError(receiveResult);
        }

        return sw.Elapsed;
    }

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Connected to {host}:{port}")]
    private partial void Connected(string host, int port);

    [LoggerMessage(EventId = 100, Level = LogLevel.Error, Message = "Failed to connect to {host}:{port}: {message}")]
    private partial void ConnectFailed(string host, int port, string message);

    [LoggerMessage(EventId = 101, Level = LogLevel.Error,
        Message = "Failed to get status for {host}:{port}: {message}")]
    private partial void StatusFailed(string host, int port, string message);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "Ping failed for {host}:{port}: {message}")]
    private partial void PingFailed(string host, int port, string message);
}

public sealed class ServerListPingClientOptions
{
    public required string Host { get; set; }
    public required int Port { get; set; }
}

public sealed record ConnectionResetError
    (string Message = "The remote host reset the connection.") : ResultError(Message);

public sealed class ServerListPingTransport : IDisposable
{
    private bool _disposed;
    private NetworkStream? _stream;
    private PipeReader? _reader;
    private PipeWriter? _writer;

    public async Task ConnectAsync(string host, int port, CancellationToken ct)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(host, port, ct);
        _stream = new NetworkStream(socket, ownsSocket: true);
        _reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        _writer = PipeWriter.Create(_stream, new StreamPipeWriterOptions(leaveOpen: true));
    }

    public async Task<Result<T>> ReceiveAsync<T>(CancellationToken ct) where T : IInbound<T>
    {
        AssertConnected();

        while (true)
        {
            ReadResult result;
            try
            {
                result = await _reader.ReadAsync(ct);
            }
            catch (IOException e) when (e.InnerException is SocketException
                                        {
                                            SocketErrorCode: SocketError.ConnectionReset,
                                        })
            {
                Disconnect(e);
                return new ConnectionResetError();
            }

            var buffer = result.Buffer;
            if (TryReadPacket(ref buffer, out var packet))
            {
                _reader.AdvanceTo(buffer.Start);
                return packet;
            }
            _reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
            {
                await _reader.CompleteAsync();
            }
        }

        bool TryReadPacket(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out T? packet)
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
    }

    public async Task<Result> SendAsync<T>(T packet, CancellationToken ct) where T : IOutbound
    {
        AssertConnected();
        WritePacket(packet);
        try
        {
            await _writer.FlushAsync(ct);
        }
        catch (IOException e) when (e.InnerException is SocketException
                                    {
                                        SocketErrorCode: SocketError.ConnectionReset,
                                    })
        {
            Disconnect(e);
            return new ConnectionResetError();
        }

        return Result.FromSuccess();

        void WritePacket(T packet)
        {
            // packet format
            // Length       VarInt (Length of Packet ID + Data)
            // Packet ID    VarInt
            // Data         Varies
            var idLength = VarInt.GetByteCount(packet.Id);
            var dataLength = packet.GetDataLength();
            var length = idLength + dataLength;
            var packetLength = VarInt.GetByteCount(length) + length;
            var buffer = _writer.GetSpan(packetLength);

            var offset = VarInt.Write(buffer, length);
            offset += VarInt.Write(buffer[offset..], packet.Id);
            packet.WriteData(buffer[offset..]);
            _writer.Advance(packetLength);
        }
    }

    [MemberNotNull(nameof(_stream), nameof(_reader), nameof(_writer))]
    public void AssertConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_stream is null || _reader is null || _writer is null)
        {
            throw new InvalidOperationException("Not connected");
        }
    }

    private void Disconnect(Exception? e = null)
    {
        _reader!.Complete(e);
        _writer!.Complete(e);
        _stream!.Dispose();

        _reader = null;
        _writer = null;
        _stream = null;
    }

    public void Dispose()
    {
        _reader?.Complete();
        _writer?.Complete();
        _stream?.Dispose();
        _disposed = true;
    }
}

public readonly struct Packet
{
    public int Id { get; init; }
    public ReadOnlySequence<byte> Data { get; init; }
}
