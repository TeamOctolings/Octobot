using Remora.Rest.Core;

namespace Boyfriend.Data;

public class MemberData {
    public MemberData(ulong id, DateTimeOffset? bannedUntil) {
        Id = id;
        BannedUntil = bannedUntil;
    }

    public ulong           Id          { get; }
    public DateTimeOffset? BannedUntil { get; set; }
    public List<Snowflake> Roles       { get; set; } = new();
}
