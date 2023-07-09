using System.ComponentModel;
using Boyfriend.Data;
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

    [Command("remind")]
    [Description("Create a reminder")]
    public async Task<Result> AddReminderAsync(TimeSpan duration, string text) {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var userResult = await _userApi.GetUserAsync(userId.Value, CancellationToken);
        if (!userResult.IsDefined(out var user))
            return Result.FromError(userResult);

        var remindAt = DateTimeOffset.UtcNow.Add(duration);

        (await _dataService.GetMemberData(guildId.Value, userId.Value, CancellationToken)).Reminders.Add(
            new Reminder {
                RemindAt = remindAt,
                Channel = channelId.Value,
                Text = text
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
