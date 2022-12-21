using Discord;

namespace Boyfriend.Data;

public record MemberData {
    public long BannedUntil;
    public ulong Id;
    public bool IsInGuild;
    public List<long> JoinedAt;
    public List<long> LeftAt;
    public long MutedUntil;
    public List<Reminder> Reminders;
    public List<ulong> Roles;

    public MemberData(IGuildUser user) {
        Id = user.Id;
        IsInGuild = true;
        JoinedAt = new List<long> { user.JoinedAt!.Value.ToUnixTimeSeconds() };
        LeftAt = new List<long>();
        Roles = user.RoleIds.ToList();
        Reminders = new List<Reminder>();
        MutedUntil = 0;
        BannedUntil = 0;
    }
}
