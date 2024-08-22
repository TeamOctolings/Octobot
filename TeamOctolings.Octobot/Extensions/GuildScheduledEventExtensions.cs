using Remora.Discord.API.Abstractions.Objects;
using Remora.Rest.Core;
using Remora.Results;

namespace TeamOctolings.Octobot.Extensions;

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
            ? Result.Success
            : new ArgumentNullError(nameof(scheduledEvent.ScheduledEndTime));
    }
}
