namespace Octobot.Data;

/// <summary>
///     Stores information about a member
/// </summary>
public sealed class MemberData
{
    public MemberData(ulong id, List<Reminder>? reminders = null, List<Warn>? warns = null)
    {
        Id = id;
        if (reminders is not null)
        {
            Reminders = reminders;
        }

        if (warns is not null)
        {
            Warns = warns;
        }
    }

    public ulong Id { get; }
    public DateTimeOffset? BannedUntil { get; set; }
    public DateTimeOffset? MutedUntil { get; set; }
    public bool Kicked { get; set; }
    public List<ulong> Roles { get; set; } = [];
    public List<Reminder> Reminders { get; } = [];
    public List<Warn> Warns { get; } = [];
}
