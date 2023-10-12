using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Extensions;

public static class GuildScheduledEventExtensions
{
    public static Result TryGetExternalEventData(this IGuildScheduledEvent scheduledEvent, out DateTimeOffset endTime,
        out string? location)
    {
        endTime = default;
        location = default;
        if (!scheduledEvent.EntityMetadata.AsOptional().IsDefined(out var metadata))
        {
            return new ArgumentNullError(nameof(scheduledEvent.EntityMetadata));
        }

        if (!metadata.Location.IsDefined(out location))
        {
            return new ArgumentNullError(nameof(metadata.Location));
        }

        return scheduledEvent.ScheduledEndTime.AsOptional().IsDefined(out endTime)
            ? Result.FromSuccess()
            : new ArgumentNullError(nameof(scheduledEvent.ScheduledEndTime));
    }
}
