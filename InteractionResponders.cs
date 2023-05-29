using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Interactivity;
using Remora.Results;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend;

public class InteractionResponders : InteractionGroup {
    private readonly FeedbackService _feedbackService;

    public InteractionResponders(FeedbackService feedbackService) {
        _feedbackService = feedbackService;
    }

    [Button("scheduled-event-details")]
    public async Task<Result> OnStatefulButtonClicked(string? state = null) {
        if (state is null) return Result.FromError(new ArgumentNullError(nameof(state)));

        var idArray = state.Split(':');
        return (Result)await _feedbackService.SendContextualAsync(
            $"https://discord.com/events/{idArray[0]}/{idArray[1]}",
            options: new FeedbackMessageOptions(MessageFlags: MessageFlags.Ephemeral));
    }
}
