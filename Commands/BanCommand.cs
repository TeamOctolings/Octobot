using System.ComponentModel;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Gateway.Events;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Commands;

public class BanCommand : CommandGroup{
    private readonly FeedbackService _feedbackService;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpCatCommands"/> class.
    /// </summary>
    /// <param name="feedbackService">The feedback service.</param>
    public BanCommand(FeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }


    [Command("ban")]
    [Description("банит пидора")]
    public async Task<IResult> BanAsync([Description("Юзер, кого банить")] IUser user, string reason) {
        var banan = new Ban(reason, user);
        var embed = new Embed(Colour: _feedbackService.Theme.Secondary, Description: "забанен нахуй");

        return (Result)await _feedbackService.SendContextualEmbedAsync(embed, ct: this.CancellationToken);

    }
}
