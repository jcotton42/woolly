namespace Woolly.Data.Models;

public sealed class PlayerServerMembership
{
    public int Id { get; init; }
    public MinecraftPlayer Player { get; init; } = null!;
    public MinecraftServer Server { get; init; } = null!;
    public required MembershipState State { get; set; }
}

public enum MembershipState
{
    Joined,
    RemovePending,
}