using Discord;

namespace Boyfriend.Data;

public record MemberData {
    public DateTimeOffset BannedUntil;
    public ulong Id;
    public bool IsInGuild;
    public List<DateTimeOffset> JoinedAt;
    public List<DateTimeOffset> LeftAt;
    public DateTimeOffset MutedUntil;
    public List<Reminder> Reminders;
    public List<ulong> Roles;

    public MemberData(IGuildUser user) {
        Id = user.Id;
        IsInGuild = true;
        JoinedAt = new List<DateTimeOffset> { user.JoinedAt!.Value };
        LeftAt = new List<DateTimeOffset>();
        Roles = user.RoleIds.ToList();
        Reminders = new List<Reminder>();
        MutedUntil = DateTimeOffset.MinValue;
        BannedUntil = DateTimeOffset.MinValue;
    }
}
