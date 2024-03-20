using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Nodes;
using JetBrains.Annotations;
using Octobot.Data;
using Octobot.Data.Options;
using Octobot.Extensions;
using Octobot.Services;
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

namespace Octobot.Commands;

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
    [
        GuildSettings.Language,
        GuildSettings.WelcomeMessage,
        GuildSettings.LeaveMessage,
        GuildSettings.ReceiveStartupMessages,
        GuildSettings.RemoveRolesOnMute,
        GuildSettings.ReturnRolesOnRejoin,
        GuildSettings.AutoStartEvents,
        GuildSettings.RenameHoistedUsers,
        GuildSettings.PublicFeedbackChannel,
        GuildSettings.PrivateFeedbackChannel,
        GuildSettings.WelcomeMessagesChannel,
        GuildSettings.EventNotificationChannel,
        GuildSettings.DefaultRole,
        GuildSettings.MuteRole,
        GuildSettings.EventNotificationRole,
        GuildSettings.EventEarlyNotificationOffset
    ];

    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly Utility _utility;

    public SettingsCommandGroup(
        ICommandContext context, GuildDataService guildData,
        IFeedbackService feedback, IDiscordRestUserAPI userApi, Utility utility)
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

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return ResultExtensions.FromError(botResult);
        }

        var cfg = await _guildData.GetSettings(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(cfg);

        return await SendSettingsListAsync(cfg, bot, page, CancellationToken);
    }

    private Task<Result> SendSettingsListAsync(JsonNode cfg, IUser bot, int page,
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
            var errorEmbed = new EmbedBuilder().WithSmallTitle(Messages.PageNotFound, bot)
                .WithDescription(string.Format(Messages.PagesAllowed, Markdown.Bold(totalPages.ToString())))
                .WithColour(ColorsList.Red)
                .Build();

            return _feedback.SendContextualEmbedResultAsync(errorEmbed, ct: ct);
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

            description.AppendBulletPointLine($"Settings{optionName}".Localized())
                .AppendSubBulletPoint(Markdown.InlineCode(optionName))
                .Append(": ").AppendLine(optionValue);
        }

        var embed = new EmbedBuilder().WithSmallTitle(Messages.SettingsListTitle, bot)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Default)
            .WithFooter(footer.ToString())
            .Build();

        return _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
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
        [Description("Setting value")] [MaxLength(512)]
        string value)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return ResultExtensions.FromError(botResult);
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return ResultExtensions.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await EditSettingAsync(AllOptions[(int)setting], value, data, channelId, executor, bot,
            CancellationToken);
    }

    private async Task<Result> EditSettingAsync(
        IOption option, string value, GuildData data, Snowflake channelId, IUser executor, IUser bot,
        CancellationToken ct = default)
    {
        var setResult = option.Set(data.Settings, value);
        if (!setResult.IsSuccess)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.SettingNotChanged, bot)
                .WithDescription(setResult.Error.Message)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        var builder = new StringBuilder();

        builder.Append(Markdown.InlineCode(option.Name))
            .Append($" {Messages.SettingIsNow} ")
            .Append(option.Display(data.Settings));
        var title = Messages.SettingSuccessfullyChanged;
        var description = builder.ToString();

        _utility.LogAction(
            data.Settings, channelId, executor, title, description, bot, ColorsList.Magenta, false, ct);

        var embed = new EmbedBuilder().WithSmallTitle(title, bot)
            .WithDescription(description)
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    /// <summary>
    ///     A slash command that resets per-guild GuildSettings.
    /// </summary>
    /// <param name="setting">The setting to reset.</param>
    /// <returns>A feedback sending result which may have succeeded.</returns>
    [Command("resetsettings")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageGuild)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageGuild)]
    [Description("Reset settings for this server")]
    [UsedImplicitly]
    public async Task<Result> ExecuteResetSettingsAsync(
        [Description("Setting to reset")] AllOptionsEnum? setting = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return ResultExtensions.FromError(botResult);
        }

        var cfg = await _guildData.GetSettings(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(cfg);

        if (setting is not null)
        {
            return await ResetSingleSettingAsync(cfg, bot, AllOptions[(int)setting], CancellationToken);
        }

        return await ResetAllSettingsAsync(cfg, bot, CancellationToken);
    }

    private async Task<Result> ResetSingleSettingAsync(JsonNode cfg, IUser bot,
        IOption option, CancellationToken ct = default)
    {
        var resetResult = option.Reset(cfg);
        if (!resetResult.IsSuccess)
        {
            return ResultExtensions.FromError(resetResult);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.SingleSettingReset, option.Name), bot)
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    private async Task<Result> ResetAllSettingsAsync(JsonNode cfg, IUser bot,
        CancellationToken ct = default)
    {
        var failedResults = new List<Result>();
        foreach (var resetResult in AllOptions.Select(option => option.Reset(cfg)))
        {
            failedResults.AddIfFailed(resetResult);
        }

        if (failedResults.Count is not 0)
        {
            return failedResults.AggregateErrors();
        }

        var embed = new EmbedBuilder().WithSmallTitle(Messages.AllSettingsReset, bot)
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
