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
public class SettingsCommandGroup : CommandGroup {
    private static readonly IOption[] AllOptions = {
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

    private readonly ICommandContext     _context;
    private readonly GuildDataService    _dataService;
    private readonly FeedbackService     _feedbackService;
    private readonly IDiscordRestUserAPI _userApi;

    public SettingsCommandGroup(
        ICommandContext context,         GuildDataService    dataService,
        FeedbackService feedbackService, IDiscordRestUserAPI userApi) {
        _context = context;
        _dataService = dataService;
        _feedbackService = feedbackService;
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
    public async Task<Result> ExecuteSettingsListAsync() {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetSettings(guildId.Value, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(cfg);

        return await SendSettingsListAsync(cfg, currentUser, CancellationToken);
    }

    private async Task<Result> SendSettingsListAsync(JsonNode cfg, IUser currentUser, CancellationToken ct = default) {
        var builder = new StringBuilder();

        foreach (var option in AllOptions) {
            builder.Append(Markdown.InlineCode(option.Name))
                .Append(": ");
            builder.AppendLine(option.Display(cfg));
        }

        var embed = new EmbedBuilder().WithSmallTitle(Messages.SettingsListTitle, currentUser)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Default)
            .Build();

        return await _feedbackService.SendContextualEmbedResultAsync(embed, ct);
    }

    /// <summary>
    /// A slash command that modifies per-guild GuildSettings.
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
        [Description("Setting value")] string value) {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var data = await _dataService.GetData(guildId.Value, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await EditSettingAsync(setting, value, data, currentUser, CancellationToken);
    }

    private async Task<Result> EditSettingAsync(
        string setting, string value, GuildData data, IUser currentUser, CancellationToken ct = default) {
        var option = AllOptions.Single(
            o => string.Equals(setting, o.Name, StringComparison.InvariantCultureIgnoreCase));

        var setResult = option.Set(data.Settings, value);
        if (!setResult.IsSuccess) {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.SettingNotChanged, currentUser)
                .WithDescription(setResult.Error.Message)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedbackService.SendContextualEmbedResultAsync(failedEmbed, ct);
        }

        var builder = new StringBuilder();

        builder.Append(Markdown.InlineCode(option.Name))
            .Append($" {Messages.SettingIsNow} ")
            .Append(option.Display(data.Settings));

        var embed = new EmbedBuilder().WithSmallTitle(Messages.SettingSuccessfullyChanged, currentUser)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Green)
            .Build();

        return await _feedbackService.SendContextualEmbedResultAsync(embed, ct);
    }
}
