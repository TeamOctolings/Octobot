namespace TeamOctolings.Octobot.Data;

/// <summary>
///     Stores information about a member
/// </summary>
public sealed class MemberData
{
    public MemberData(ulong id, List<Reminder>? reminders = null)
    {
        Id = id;
        if (reminders is not null)
        {
            Reminders = reminders;
        }
    }

    public ulong Id { get; }
    public DateTimeOffset? BannedUntil { get; set; }
    public DateTimeOffset? MutedUntil { get; set; }
    public bool Kicked { get; set; }
    public List<ulong> Roles { get; set; } = [];
    public List<Reminder> Reminders { get; } = [];
}
