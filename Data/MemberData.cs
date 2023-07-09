using Remora.Rest.Core;

namespace Boyfriend.Data;

/// <summary>
///     Stores information about a member
/// </summary>
public class MemberData {
    public MemberData(ulong id, DateTimeOffset? bannedUntil) {
        Id = id;
        BannedUntil = bannedUntil;
    }

    public ulong           Id          { get; }
    public DateTimeOffset? BannedUntil { get; set; }
    public List<Snowflake> Roles       { get; set; } = new();
    public List<Reminder>  Reminders   { get; }      = new();
}
