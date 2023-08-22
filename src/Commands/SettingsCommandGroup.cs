using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Boyfriend.Data;
using Boyfriend.Data.Options;
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
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend.Commands;

/// <summary>
///     Handles the commands to list and modify per-guild settings: /settings and /settings list.
/// </summary>
[UsedImplicitly]
public class SettingsCommandGroup : CommandGroup
{
    /// <summary>
    ///     Represents all options as an array of objects implementing <see cref="IOption" />.
    /// </summary>
    /// <remarks>
    ///     WARNING: If you update this array in any way, you must also update <see cref="AllOptionsEnum" /> and make sure
    ///     that the orders match.
    /// </remarks>
    private static readonly IOption[] AllOptions =
    {
        GuildSettings.Language,
        GuildSettings.WelcomeMessage,
        GuildSettings.ReceiveStartupMessages,
        GuildSettings.RemoveRolesOnMute,
        GuildSettings.ReturnRolesOnRejoin,
        GuildSettings.AutoStartEvents,
        GuildSettings.RenameHoistedUsers,
        GuildSettings.PublicFeedbackChannel,
        GuildSettings.PrivateFeedbackChannel,
        GuildSettings.EventNotificationChannel,
        GuildSettings.DefaultRole,
        GuildSettings.MuteRole,
        GuildSettings.EventNotificationRole,
        GuildSettings.EventEarlyNotificationOffset
    };

    private readonly ICommandContext _context;
    private readonly FeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly UtilityService _utility;

    public SettingsCommandGroup(
        ICommandContext context, GuildDataService guildData,
        FeedbackService feedback, IDiscordRestUserAPI userApi, UtilityService utility)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _userApi = userApi;
        _utility = utility;
    }

    /// <summary>
    ///     A slash command that sends a page from the list of current GuildSettings.
    /// </summary>
    /// <param name="page">The number of the page to send.</param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("listsettings")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageGuild)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageGuild)]
    [Description("Shows settings list for this server")]
    [UsedImplicitly]
    public async Task<Result> ExecuteListSettingsAsync(
        [Description("Settings list page")] [MinValue(1)]
        int page)
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

        return await SendSettingsListAsync(cfg, currentUser, page, CancellationToken);
    }

    private async Task<Result> SendSettingsListAsync(JsonNode cfg, IUser currentUser, int page,
        CancellationToken ct = default)
    {
        var description = new StringBuilder();
        var footer = new StringBuilder();

        const int optionsPerPage = 10;

        var totalPages = (AllOptions.Length + optionsPerPage - 1) / optionsPerPage;
        var lastOptionOnPage = Math.Min(optionsPerPage * page, AllOptions.Length);
        var firstOptionOnPage = optionsPerPage * page - optionsPerPage;

        if (firstOptionOnPage >= AllOptions.Length)
        {
            var errorEmbed = new EmbedBuilder().WithSmallTitle(Messages.PageNotFound, currentUser)
                .WithDescription(string.Format(Messages.PagesAllowed, Markdown.Bold(totalPages.ToString())))
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(errorEmbed, ct);
        }

        footer.Append($"{Messages.Page} {page}/{totalPages} ");
        for (var i = 0; i < totalPages; i++)
        {
            footer.Append(i + 1 == page ? "●" : "○");
        }

        for (var i = firstOptionOnPage; i < lastOptionOnPage; i++)
        {
            var optionName = AllOptions[i].Name;
            var optionValue = AllOptions[i].Display(cfg);

            description.AppendLine($"- {$"Settings{optionName}".Localized()}")
                .Append($" - {Markdown.InlineCode(optionName)}: ")
                .AppendLine(optionValue);
        }

        var embed = new EmbedBuilder().WithSmallTitle(Messages.SettingsListTitle, currentUser)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Default)
            .WithFooter(footer.ToString())
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }

    /// <summary>
    ///     A slash command that modifies per-guild GuildSettings.
    /// </summary>
    /// <param name="setting">The setting to modify.</param>
    /// <param name="value">The new value of the setting.</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("editsettings")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageGuild)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageGuild)]
    [Description("Change settings for this server")]
    [UsedImplicitly]
    public async Task<Result> ExecuteEditSettingsAsync(
        [Description("The setting whose value you want to change")]
        AllOptionsEnum setting,
        [Description("Setting value")] string value)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var userId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
        {
            return Result.FromError(currentUserResult);
        }

        var userResult = await _userApi.GetUserAsync(userId, CancellationToken);
        if (!userResult.IsDefined(out var user))
        {
            return Result.FromError(userResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await EditSettingAsync(AllOptions[(int)setting], value, data, channelId, user, currentUser,
            CancellationToken);
    }

    private async Task<Result> EditSettingAsync(
        IOption option, string value, GuildData data, Snowflake channelId, IUser user, IUser currentUser,
        CancellationToken ct = default)
    {
        var setResult = option.Set(data.Settings, value);
        if (!setResult.IsSuccess)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.SettingNotChanged, currentUser)
                .WithDescription(setResult.Error.Message)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var builder = new StringBuilder();

        builder.Append(Markdown.InlineCode(option.Name))
            .Append($" {Messages.SettingIsNow} ")
            .Append(option.Display(data.Settings));
        var title = Messages.SettingSuccessfullyChanged;
        var description = builder.ToString();

        var logResult = _utility.LogActionAsync(
            data.Settings, channelId, user, title, description, currentUser, ColorsList.Magenta, false, ct);
        if (!logResult.IsSuccess)
        {
            return Result.FromError(logResult.Error);
        }

        var embed = new EmbedBuilder().WithSmallTitle(title, currentUser)
            .WithDescription(description)
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }
}
