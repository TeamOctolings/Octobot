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
using Remora.Results;

namespace Boyfriend.Commands;

/// <summary>
///     Handles the commands to list and modify per-guild settings: /settings and /settings list.
/// </summary>
[UsedImplicitly]
public class SettingsCommandGroup : CommandGroup
{
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

    public SettingsCommandGroup(
        ICommandContext context, GuildDataService guildData,
        FeedbackService feedback, IDiscordRestUserAPI userApi)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _userApi = userApi;
    }

    /// <summary>
    ///     A slash command that lists current per-guild GuildSettings.
    /// </summary>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("settingslist")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageGuild)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageGuild)]
    [Description("Shows settings list for this server")]
    [UsedImplicitly]
    public async Task<Result> ExecuteSettingsListAsync(
        [Description("Settings list page")] int page)
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
    [Command("settings")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageGuild)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageGuild)]
    [Description("Change settings for this server")]
    [UsedImplicitly]
    public async Task<Result> ExecuteSettingsAsync(
        [Description("The setting whose value you want to change")]
        string setting,
        [Description("Setting value")] string value)
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

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await EditSettingAsync(setting, value, data, currentUser, CancellationToken);
    }

    private async Task<Result> EditSettingAsync(
        string setting, string value, GuildData data, IUser currentUser, CancellationToken ct = default)
    {
        var option = AllOptions.Single(
            o => string.Equals(setting, o.Name, StringComparison.InvariantCultureIgnoreCase));

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

        var embed = new EmbedBuilder().WithSmallTitle(Messages.SettingSuccessfullyChanged, currentUser)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct);
    }
}
