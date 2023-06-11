namespace Boyfriend.Data;

public class MemberData {
    public MemberData(ulong id, DateTimeOffset? bannedUntil) {
        Id = id;
        BannedUntil = bannedUntil;
    }

    public ulong           Id          { get; }
    public DateTimeOffset? BannedUntil { get; set; }
}
