using JetBrains.Annotations;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace Boyfriend;

/// <summary>
///     Handles responding to various interactions.
/// </summary>
[UsedImplicitly]
public class InteractionResponders : InteractionGroup
{
    private readonly FeedbackService _feedback;

    public InteractionResponders(FeedbackService feedback)
    {
        _feedback = feedback;
    }

    /// <summary>
    ///     A button that will output an ephemeral embed containing the information about a scheduled event.
    /// </summary>
    /// <param name="state">The ID of the guild and scheduled event, encoded as "guildId:eventId".</param>
    /// <returns>An ephemeral feedback sending result which may or may not have succeeded.</returns>
    [Button("scheduled-event-details")]
    [UsedImplicitly]
    public async Task<Result> OnStatefulButtonClicked(string? state = null)
    {
        if (state is null)
        {
            return new ArgumentNullError(nameof(state));
        }

        var idArray = state.Split(':');
        return (Result)await _feedback.SendContextualAsync(
            $"https://discord.com/events/{idArray[0]}/{idArray[1]}",
            options: new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral), ct: CancellationToken);
    }
}