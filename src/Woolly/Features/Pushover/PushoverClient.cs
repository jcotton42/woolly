using FluentValidation;

using Microsoft.Extensions.Options;

using Woolly.Infrastructure;

namespace Woolly.Features.Pushover;

public sealed class PushoverClient
{
    private const string Url = "https://api.pushover.net/1/messages.json";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiToken;
    private readonly string _userKey;

    public PushoverClient(IHttpClientFactory httpClientFactory, IOptions<PushoverClientOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _apiToken = options.Value.ApiToken;
        _userKey = options.Value.UserKey;
    }

    public async Task SendNotificationAsync(string title, string message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        var client = _httpClientFactory.CreateClient();

        var body = new Dictionary<string, string>
        {
            ["token"] = _apiToken, ["user"] = _userKey, ["title"] = title, ["message"] = message,
        };

        var response = await client.PostAsync(Url, new FormUrlEncodedContent(body), ct);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class PushoverClientOptions
{
    public required string ApiToken { get; set; }
    public required string UserKey { get; set; }

    public sealed class Validator : AbstractOptionsValidator<PushoverClientOptions>
    {
        public Validator()
        {
            RuleFor(o => o.ApiToken).NotEmpty();
            RuleFor(o => o.UserKey).NotEmpty();
        }
    }
}
