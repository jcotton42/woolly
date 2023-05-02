using FluentValidation;

using Remora.Rest.Core;

using Woolly.Infrastructure;

namespace Woolly;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public required string Token { get; set; }
    public required string AppId { get; set; }
    public Snowflake? TestServerId { get; set; }

    public sealed class Validator : AbstractOptionsValidator<DiscordOptions>
    {
        public Validator()
        {
            RuleFor(o => o.Token).NotEmpty();
            RuleFor(o => o.AppId).NotEmpty();
        }
    }
}
