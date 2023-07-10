using System.ComponentModel;
using Boyfriend.Data;
using Boyfriend.locale;
using Boyfriend.Services;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Results;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace Boyfriend.Commands;

/// <summary>
///     Handles the command to manage reminders: /remind
/// </summary>
public class RemindCommandGroup : CommandGroup {
    private readonly ICommandContext     _context;
    private readonly GuildDataService    _dataService;
    private readonly FeedbackService     _feedbackService;
    private readonly IDiscordRestUserAPI _userApi;

    public RemindCommandGroup(
        ICommandContext     context, GuildDataService dataService, FeedbackService feedbackService,
        IDiscordRestUserAPI userApi) {
        _context = context;
        _dataService = dataService;
        _feedbackService = feedbackService;
        _userApi = userApi;
    }

    /// <summary>
    ///     A slash command that schedules a reminder with the specified text.
    /// </summary>
    /// <param name="in">The period of time which must pass before the reminder will be sent.</param>
    /// <param name="message">The text of the reminder.</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("remind")]
    [Description("Create a reminder")]
    public async Task<Result> AddReminderAsync(
        [Description("After what period of time mention the reminder")]
        TimeSpan @in,
        [Description("Reminder message")] string message) {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
        if (!userResult.IsDefined(out var user))
            return Result.FromError(userResult);

        var remindAt = DateTimeOffset.UtcNow.Add(@in);

        (await _dataService.GetMemberData(guildId.Value, userId.Value, CancellationToken)).Reminders.Add(
            new Reminder {
                At = remindAt,
                Channel = channelId.Value.Value,
                Text = message
            });

        var embed = new EmbedBuilder().WithSmallTitle(string.Format(Messages.ReminderCreated, user.GetTag()), user)
            .WithDescription(string.Format(Messages.DescriptionReminderCreated, Markdown.Timestamp(remindAt)))
            .WithColour(ColorsList.Green)
            .Build();

        if (!embed.IsDefined(out var built))
            return Result.FromError(embed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }
}
