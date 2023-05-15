using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Rest.Core;

namespace Woolly.Extensions;

public static class OperationContextExtensions
{
    public static Snowflake GetUserId(this IOperationContext context)
        => context.TryGetUserID(out var id)
            ? id.Value
            : throw new InvalidOperationException("No user is available for this context.");

    public static Snowflake GetGuildId(this IOperationContext context)
        => context.TryGetGuildID(out var id)
            ? id.Value
            : throw new InvalidOperationException("No guild is available for this context.");

    public static Snowflake GetChannelId(this IOperationContext context)
        => context.TryGetChannelID(out var id)
            ? id.Value
            : throw new InvalidOperationException("No channel is available for this context.");
}
