using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Woolly.Features.Rcon;

public interface IRconConnection : IDisposable
{
    bool IsConnected { get; }
    Task ConnectAsync(string host, int port, CancellationToken token);
    Task<RconPacket?> ReceiveAsync(CancellationToken token);
    Task SendAsync(RconPacket packet, CancellationToken token);
}

public sealed class RconConnection : IRconConnection
{
    private readonly byte[] _sendBuffer;

    private bool _isDisposed;
    private PipeReader? _pipe;
    private NetworkStream? _stream;

    public bool IsConnected { get; private set; }

    public RconConnection()
    {
        // maximum client to server packet length per https://wiki.vg/RCON#Fragmentation
        _sendBuffer = ArrayPool<byte>.Shared.Rent(1460);
    }

    public async Task ConnectAsync(string host, int port, CancellationToken token)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(host, port, token);
        _stream = new NetworkStream(socket, ownsSocket: true);
        _pipe = PipeReader.Create(_stream, new StreamPipeReaderOptions(leaveOpen: true));
        IsConnected = true;
    }

    public async Task<RconPacket?> ReceiveAsync(CancellationToken token)
    {
        AssertConnected();

        while (true)
        {
            var result = await _pipe.ReadAsync(token);
            var buffer = result.Buffer;
            if (RconPacket.TryRead(buffer, out var packet, out var consumed))
            {
                _pipe.AdvanceTo(consumed: consumed, examined: consumed);
                return packet;
            }

            _pipe.AdvanceTo(consumed: buffer.Start, examined: buffer.End);
            if (result.IsCompleted)
            {
                IsConnected = false;
                return null;
            }
        }
    }

    public async Task SendAsync(RconPacket packet, CancellationToken token)
    {
        AssertConnected();
        var couldWrite = packet.TryWrite(_sendBuffer, out var length);
        Debug.Assert(couldWrite,
            $"Packet was too long for buffer. Packet length {length}. Buffer length {_sendBuffer.Length}");
        await _stream.WriteAsync(_sendBuffer.AsMemory(..length), token);
    }

    [MemberNotNull(nameof(_pipe), nameof(_stream))]
    private void AssertConnected()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException("This connection has been disposed and may not be used anymore.");
        }

        if (!IsConnected || _pipe is null || _stream is null) throw new InvalidOperationException("Not connected.");
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        IsConnected = false;
        _pipe?.Complete();
        _stream?.Dispose();
        ArrayPool<byte>.Shared.Return(_sendBuffer, clearArray: true);
    }
}
