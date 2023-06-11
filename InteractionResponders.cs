using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Results;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend;

/// <summary>
///     Handles responding to various interactions.
/// </summary>
public class InteractionResponders : InteractionGroup {
    private readonly FeedbackService _feedbackService;

    public InteractionResponders(FeedbackService feedbackService) {
        _feedbackService = feedbackService;
    }

    /// <summary>
    ///     A button that will output an ephemeral embed containing the information about a scheduled event.
    /// </summary>
    /// <param name="state">The ID of the guild and scheduled event, encoded as "guildId:eventId".</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Button("scheduled-event-details")]
    public async Task<Result> OnStatefulButtonClicked(string? state = null) {
        if (state is null) return Result.FromError(new ArgumentNullError(nameof(state)));

        var idArray = state.Split(':');
        return (Result)await _feedbackService.SendContextualAsync(
            $"https://discord.com/events/{idArray[0]}/{idArray[1]}",
            options: new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral));
    }
}
