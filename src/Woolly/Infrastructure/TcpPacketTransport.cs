using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;

using Remora.Results;

namespace Woolly.Infrastructure;

public delegate bool PacketReader<T>(ref ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out T? packet);
public delegate void PacketWriter<in T>(IBufferWriter<byte> writer, T packet);

public sealed record ConnectionResetError
    (string Message = "The remote host reset the connection.") : ResultError(Message);

public interface ITcpPacketTransport : IDisposable
{
    Task ConnectAsync(string host, int port, CancellationToken ct);
    Task<Result<T>> ReceiveAsync<T>(PacketReader<T> tryReadPacket, CancellationToken ct) where T : notnull;
    Task<Result> SendAsync<T>(T rconPacket, PacketWriter<T> writePacket, CancellationToken ct) where T : notnull;
    void AssertConnected();
}

/// <summary>
/// A generic TCP packet transport.
/// </summary>
/// <remarks>
/// This class may be read from on up to one thread at a time, and written to on up to one thread at a time. Using more
/// than one thread at a time for reading or writing is unsafe.
/// </remarks>
public sealed class TcpPacketTransport : ITcpPacketTransport, IDisposable
{
    private bool _disposed;
    private NetworkStream? _stream;
    private PipeReader? _reader;
    private PipeWriter? _writer;

    public async Task ConnectAsync(string host, int port, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(host, port, ct);
        _stream = new NetworkStream(socket, ownsSocket: true);
        _reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        _writer = PipeWriter.Create(_stream, new StreamPipeWriterOptions(leaveOpen: true));
    }

    public async Task<Result<T>> ReceiveAsync<T>(PacketReader<T> tryReadPacket, CancellationToken ct) where T : notnull
    {
        AssertConnected();

        while (true)
        {
            ReadResult result;
            try
            {
                result = await _reader.ReadAsync(ct);
            }
            catch (IOException e) when (ShouldReconnect(e))
            {
                Disconnect(e);
                return new ConnectionResetError();
            }

            var buffer = result.Buffer;
            if (tryReadPacket(ref buffer, out var packet))
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
    }

    public async Task<Result> SendAsync<T>(T rconPacket, PacketWriter<T> writePacket, CancellationToken ct)
        where T : notnull
    {
        AssertConnected();
        writePacket(_writer, rconPacket);
        try
        {
            await _writer.FlushAsync(ct);
        }
        catch (IOException e) when (ShouldReconnect(e))
        {
            Disconnect(e);
            return new ConnectionResetError();
        }

        return Result.FromSuccess();
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

    private static bool ShouldReconnect(IOException e) => e.InnerException is SocketException
    {
        SocketErrorCode: SocketError.ConnectionAborted or SocketError.ConnectionReset,
    };
}
