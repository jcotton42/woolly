using System.Buffers;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Remora.Rest.Core;
using Remora.Results;

using Woolly.Data;
using Woolly.Infrastructure;

namespace Woolly.Features.Rcon;

// TODO, get this reviewed
public sealed class RconClientFactory : IDisposable
{
    private readonly WoollyContext _db;
    private readonly SemaphoreSlim _semaphore;
    private readonly IServiceProvider _serviceProvider;
    // TODO, case insensitivity on the name part of the key?
    private readonly Dictionary<(Snowflake GuildId, string Name), RconClient> _clients;

    public RconClientFactory(WoollyContext db, IServiceProvider serviceProvider)
    {
        _db = db;
        _serviceProvider = serviceProvider;

        _semaphore = new SemaphoreSlim(1, 1);
        _clients = new Dictionary<(Snowflake GuildId, string Name), RconClient>();
    }

    public async Task<Result<RconClient>> GetClientAsync(Snowflake guildId, string name, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            if (_clients.TryGetValue((guildId, name), out var client)) return client;
            var server =
                await _db.MinecraftServers.FirstOrDefaultAsync(s => s.GuildId == guildId || s.Name == name, ct);
            if (server is null)
            {
                return new NotFoundError($"No Minecraft server named `{name}` is registered in this guild.");
            }

            var options = new RconOptions
            {
                Hostname = server.Host, Port = server.RconPort, Password = server.RconPassword,
            };

            client = ActivatorUtilities.CreateInstance<RconClient>(
                _serviceProvider,
                () => InvalidateClient(guildId, name),
                options);

            var connect = await client.ConnectAsync(ct);
            if (!connect.IsSuccess)
            {
                client.Dispose();
                return Result<RconClient>.FromError(connect);
            }

            _clients.Add((guildId, name), client);
            return client;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void InvalidateClient(Snowflake guildId, string name)
    {
        _semaphore.Wait();
        try
        {
            if (_clients.Remove((guildId, name), out var client))
            {
                // TODO this feels like a terrible idea, what if someone else is using it atm?
                client.Dispose();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
    }
}

public sealed class RconClient : IDisposable
{
    private static readonly string[] WhitelistSplits = {":", ", ", " and "};

    private readonly SemaphoreSlim _semaphore;
    private readonly ITcpPacketTransport _transport;
    private readonly Action _invalidate;
    private readonly RconOptions _options;

    private bool _isConnected;
    private int _disposed;

    internal int NextId = 1;

    public RconClient(ITcpPacketTransport transport, Action invalidate, RconOptions options)
    {
        _semaphore = new SemaphoreSlim(1, 1);
        _transport = transport;
        _invalidate = invalidate;
        _options = options;
    }

    internal async Task<Result> ConnectAsync(CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            await _transport.ConnectAsync(_options.Hostname, _options.Port, ct);
            var loginPacket =
                new RconPacket { Id = NextId++, Type = RconPacketType.Login, Payload = _options.Password, };
            await _transport.SendAsync(loginPacket, WritePacket, ct);
            var loginReplyResult = await _transport.ReceiveAsync<RconPacket>(TryReadPacket, ct);
            if (!loginReplyResult.IsDefined(out var loginReply)) return (Result)loginReplyResult;

            if (loginPacket.Id != loginReply.Id)
            {
                return new ArgumentInvalidError(nameof(_options.Password), "Invalid password.");
            }

            _isConnected = true;
            return Result.FromSuccess();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Result<bool>> AddToWhitelistAsync(string username, CancellationToken ct) =>
        (await SendCommandAsync($"whitelist add {username}", ct))
        .Map(output => output.StartsWith("added", StringComparison.OrdinalIgnoreCase));

    public async Task<Result<bool>> RemoveFromWhitelistAsync(string username, CancellationToken ct) =>
        (await SendCommandAsync($"whitelist remove {username}", ct))
        .Map(output => output.StartsWith("removed", StringComparison.OrdinalIgnoreCase));

    public async Task<Result<string[]>> ListWhitelistAsync(CancellationToken ct) =>
        (await SendCommandAsync("whitelist list", ct))
        .Map(output =>
            output
                .Split(WhitelistSplits, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Skip(1) // skip the part before the colon
                .ToArray());

    public async Task<Result<bool>> OpAsync(string username, CancellationToken ct) =>
        (await SendCommandAsync($"op {username}", ct))
        .Map(output => output.StartsWith("made", StringComparison.OrdinalIgnoreCase)
                       || output.StartsWith("opped", StringComparison.OrdinalIgnoreCase));

    public async Task<Result<bool>> DeOpAsync(string username, CancellationToken ct) =>
        (await SendCommandAsync($"deop {username}", ct))
        .Map(output => output.StartsWith("made", StringComparison.OrdinalIgnoreCase)
                       || output.StartsWith("de-opped", StringComparison.OrdinalIgnoreCase));

    public async Task<Result> SayAsync(string message, CancellationToken ct) =>
        (Result)await SendCommandAsync($"say {message}", ct);

    public async Task<Result<string>> SendCommandAsync(string command, CancellationToken ct)
    {
        AssertConnected();
        await _semaphore.WaitAsync(ct);
        try
        {
            // Responses from an RCON server can be fragmented across multiple packets, but there's no standard end of
            // response flag. So send an "end" packet with a different ID, which will let us know when we've finished
            // the reply.
            var commandPacketId = NextId++;
            var endPacketId = NextId++;
            var commandPacket =
                new RconPacket { Id = commandPacketId, Type = RconPacketType.Command, Payload = command };
            var endPacket = new RconPacket { Id = endPacketId, Type = RconPacketType.Command, Payload = "" };

            await _transport.SendAsync(commandPacket, WritePacket, ct);
            await _transport.SendAsync(endPacket, WritePacket, ct);
            var output = new StringBuilder();

            while (true)
            {
                switch (await _transport.ReceiveAsync<RconPacket>(TryReadPacket, ct))
                {
                    case { IsSuccess: false } result:
                        _invalidate();
                        return Result<string>.FromError(result);
                    case { Entity.Id: < 0 }:
                        throw new InvalidOperationException("Send called before login");
                    case { Entity.Id: var id } when id == endPacketId:
                        goto end;
                    case { Entity: { Id: var id, Payload: var chunk } } when id == commandPacketId:
                        output.Append(chunk);
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected response ID");
                }
            }
            end:

            return output.ToString();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static bool TryReadPacket(ref ReadOnlySequence<byte> buffer, out RconPacket packet) =>
        RconPacket.TryRead(ref buffer, out packet);

    private static void WritePacket(IBufferWriter<byte> writer, RconPacket packet) => packet.Write(writer);

    private void AssertConnected()
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        if (!_isConnected) throw new InvalidOperationException("Client was not connected.");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        _transport.Dispose();
        _semaphore.Dispose();
    }
}
