using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

using Remora.Results;

namespace Woolly.Infrastructure;

public sealed class MojangApi : IDisposable
{
    // TODO rate limiting
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RateLimiter _usernameToProfileRateLimiter;
    private readonly RateLimiter _uuidToProfileRateLimiter;

    public MojangApi(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;

        _usernameToProfileRateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            Window = TimeSpan.FromMinutes(10),
            SegmentsPerWindow = 10,
            PermitLimit = 600,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 50,
        });

        _uuidToProfileRateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 2,
            PermitLimit = 200,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 50,
        });
    }

    public async Task<Result<MinecraftProfile>> GetProfileFromUsernameAsync(string username, CancellationToken ct)
    {
        using var lease = await _usernameToProfileRateLimiter.AcquireAsync(1, ct);
        if (!lease.IsAcquired)
        {
            return new RateLimitError(
                "Profile lookup by username is currently rate-limited. Please try again in a few minutes.");
        }

        using var client = _httpClientFactory.CreateClient();

        var response = await client.GetAsync($"https://api.mojang.com/users/profiles/minecraft/{username}", ct);
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
        {
            return new NotFoundError($"No Minecraft player named `{username}` exists.");
        }
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MinecraftProfile>(cancellationToken: ct))!;
    }

    public async Task<Result<List<MinecraftProfile>>> GetProfilesFromUsernamesAsync(ICollection<string> usernames,
        CancellationToken ct)
    {
        const int chunkSize = 10;

        using var lease =
            await _usernameToProfileRateLimiter.AcquireAsync((int)Math.Ceiling(usernames.Count * 1.0 / chunkSize), ct);
        if (!lease.IsAcquired)
        {
            return new RateLimitError(
                "Profile lookup by username is currently rate-limited. Please try again in a few minutes.");
        }

        using var client = _httpClientFactory.CreateClient();
        var profiles = new List<MinecraftProfile>();

        foreach (var chunk in usernames.Chunk(chunkSize))
        {
            var response = await client.PostAsJsonAsync("https://api.mojang.com/profiles/minecraft", chunk, ct);
            response.EnsureSuccessStatusCode();

            profiles.AddRange(
                (await response.Content.ReadFromJsonAsync<List<MinecraftProfile>>(cancellationToken: ct))!);
        }

        return profiles;
    }

    public async Task<Result<MinecraftProfile>> GetProfileFromUuidAsync(string uuid, CancellationToken ct)
    {
        using var lease = await _uuidToProfileRateLimiter.AcquireAsync(1, ct);
        if (!lease.IsAcquired)
        {
            return new RateLimitError(
                "Profile lookup by UUID is currently rate-limited. Please try again in a few minutes.");
        }

        using var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync($"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}", ct);
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
        {
            return new NotFoundError($"No Minecraft player with UUID {uuid} exists.");
        }
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MinecraftProfile>(cancellationToken: ct))!;
    }

    public void Dispose()
    {
        _usernameToProfileRateLimiter.Dispose();
        _uuidToProfileRateLimiter.Dispose();
    }
}

public sealed class MinecraftProfile
{
    [JsonPropertyName("id")]
    public required string Uuid { get; init; }

    [JsonPropertyName("name")]
    public required string Username { get; init; }
}
