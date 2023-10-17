namespace Octobot.Data;

/// <summary>
///     Stores information about a member
/// </summary>
public sealed class MemberData
{
    public MemberData(ulong id, DateTimeOffset? bannedUntil = null, List<Reminder>? reminders = null)
    {
        Id = id;
        BannedUntil = bannedUntil;
        if (reminders is not null)
        {
            Reminders = reminders;
        }
    }

    public ulong Id { get; }
    public DateTimeOffset? BannedUntil { get; set; }
    public DateTimeOffset? MutedUntil { get; set; }
    public List<ulong> Roles { get; set; } = new();
    public List<Reminder> Reminders { get; } = new();
}
