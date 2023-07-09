using System.ComponentModel;
using System.Text;
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
///     Handles the command to show information about this bot: /about.
/// </summary>
public class AboutCommandGroup : CommandGroup {
    private static readonly string[]            Developers = { "Octol1ttle", "mctaylors", "neroduckale" };
    private readonly        ICommandContext     _context;
    private readonly        GuildDataService    _dataService;
    private readonly        FeedbackService     _feedbackService;
    private readonly        IDiscordRestUserAPI _userApi;

    public AboutCommandGroup(
        ICommandContext context,         GuildDataService    dataService,
        FeedbackService feedbackService, IDiscordRestUserAPI userApi) {
        _context = context;
        _dataService = dataService;
        _feedbackService = feedbackService;
        _userApi = userApi;
    }

    /// <summary>
    ///     A slash command that shows information about this bot.
    /// </summary>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("about")]
    [Description("Shows Boyfriend's developers")]
    public async Task<Result> SendAboutBotAsync() {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.GetCulture();

        var builder = new StringBuilder().AppendLine(Markdown.Bold(Messages.AboutTitleDevelopers));
        foreach (var dev in Developers)
            builder.AppendLine($"@{dev} — {$"AboutDeveloper@{dev}".Localized()}");

        builder.AppendLine()
            .AppendLine(Markdown.Bold(Messages.AboutTitleWiki))
            .AppendLine("https://github.com/TeamOctolings/Boyfriend/wiki");

        var embed = new EmbedBuilder().WithSmallTitle(Messages.AboutBot, currentUser)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Cyan)
            .WithImageUrl(
                "https://media.discordapp.net/attachments/837385840946053181/1125009665592393738/boyfriend.png")
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }
}
