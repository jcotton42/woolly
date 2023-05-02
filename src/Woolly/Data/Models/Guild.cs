using Remora.Rest.Core;

namespace Woolly.Data.Models;

public sealed class Guild
{
    public required Snowflake Id { get; init; }
    public required string Name { get; set; }
}
