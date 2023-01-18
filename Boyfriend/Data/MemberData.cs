using System.Text.Json.Serialization;
using Discord;

namespace Boyfriend.Data;

public record MemberData {
    public DateTimeOffset? BannedUntil;
    public ulong Id;
    public bool IsInGuild;
    public List<DateTimeOffset> JoinedAt;
    public List<DateTimeOffset> LeftAt;
    public DateTimeOffset? MutedUntil;
    public List<Reminder> Reminders;
    public List<ulong> Roles;

    [JsonConstructor]
    public MemberData(DateTimeOffset? bannedUntil, ulong id, bool isInGuild, List<DateTimeOffset> joinedAt,
        List<DateTimeOffset> leftAt, DateTimeOffset? mutedUntil, List<Reminder> reminders, List<ulong> roles) {
        BannedUntil = bannedUntil;
        Id = id;
        IsInGuild = isInGuild;
        JoinedAt = joinedAt;
        LeftAt = leftAt;
        MutedUntil = mutedUntil;
        Reminders = reminders;
        Roles = roles;
    }

    public MemberData(IGuildUser user) {
        Id = user.Id;
        IsInGuild = true;
        JoinedAt = new List<DateTimeOffset> { user.JoinedAt!.Value };
        LeftAt = new List<DateTimeOffset>();
        Roles = user.RoleIds.ToList();
        Roles.Remove(user.Guild.Id);
        Reminders = new List<Reminder>();
    }
}
