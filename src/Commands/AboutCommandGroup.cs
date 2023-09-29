using System.ComponentModel;
using System.Text;
using Boyfriend.Data;
using Boyfriend.Services;
using JetBrains.Annotations;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Conditions;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Commands;

/// <summary>
///     Handles the command to show information about this bot: /about.
/// </summary>
[UsedImplicitly]
public class AboutCommandGroup : CommandGroup
{
    private static readonly (string Username, Snowflake Id)[] Developers =
    {
        ("Octol1ttle", new Snowflake(504343489664909322)),
        ("mctaylors", new Snowflake(326642240229474304)),
        ("neroduckale", new Snowflake(474943797063843851))
    };

    private readonly ICommandContext _context;
    private readonly FeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly IDiscordRestGuildAPI _guildApi;

    public AboutCommandGroup(
        ICommandContext context, GuildDataService guildData,
        FeedbackService feedback, IDiscordRestUserAPI userApi,
        IDiscordRestGuildAPI guildApi)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _userApi = userApi;
        _guildApi = guildApi;
    }

    /// <summary>
    ///     A slash command that shows information about this bot.
    /// </summary>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("about")]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [Description("Shows Boyfriend's developers")]
    [UsedImplicitly]
    public async Task<Result> ExecuteAboutAsync()
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
        {
            return Result.FromError(currentUserResult);
        }

        var cfg = await _guildData.GetSettings(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(cfg);

        return await SendAboutBotAsync(currentUser, guildId, CancellationToken);
    }

    private async Task<Result> SendAboutBotAsync(IUser currentUser, Snowflake guildId, CancellationToken ct = default)
    {
        var builder = new StringBuilder().Append("### ").AppendLine(Messages.AboutTitleDevelopers);
        foreach (var dev in Developers)
        {
            var tag = $"@{dev.Username}";
            var guildMemberResult = await _guildApi.GetGuildMemberAsync(
                guildId, dev.Id, ct);
            if (guildMemberResult.IsSuccess)
            {
                tag = $"<@{dev.Id}>";
            }

            builder.AppendLine($"- {tag} — {$"AboutDeveloper@{dev.Username}".Localized()}");
        }

        builder.Append($"### [{Messages.AboutTitleRepository}](https://github.com/LabsDevelopment/Boyfriend)");

        var embed = new EmbedBuilder().WithSmallTitle(Messages.AboutBot, currentUser)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Cyan)
            .WithImageUrl("https://mctaylors.ddns.net/cdn/boyfriend-banner-light.png")
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }
}
