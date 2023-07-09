using System.ComponentModel;
using System.Reflection;
using System.Text;
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
///     Handles the commands to list and modify per-guild settings: /settings and /settings list.
/// </summary>
public class SettingsCommandGroup : CommandGroup {
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
    ///     A slash command that lists current per-guild settings.
    /// </summary>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("settingslist")]
    [Description("Shows settings list for this server")]
    public async Task<Result> SendSettingsListAsync() {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.GetCulture();

        var builder = new StringBuilder();

        foreach (var setting in typeof(GuildConfiguration).GetProperties()) {
            builder.Append(Markdown.InlineCode(setting.Name))
                .Append(": ");
            var something = setting.GetValue(cfg);
            if (something!.GetType() == typeof(List<GuildConfiguration.NotificationReceiver>)) {
                var list = (something as List<GuildConfiguration.NotificationReceiver>);
                builder.AppendLine(string.Join(", ", list!.Select(v => Markdown.InlineCode(v.ToString()))));
            } else { builder.AppendLine(Markdown.InlineCode(something.ToString()!)); }
        }

        var embed = new EmbedBuilder().WithSmallTitle(Messages.SettingsListTitle, currentUser)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Default)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }

    /// <summary>
    /// A slash command that modifies per-guild settings.
    /// </summary>
    /// <param name="setting">The setting to modify.</param>
    /// <param name="value">The new value of the setting.</param>
    /// <returns>A feedback sending result which may or may not have succeeded.</returns>
    [Command("settings")]
    [Description("Change settings for this server")]
    public async Task<Result> EditSettingsAsync(
        [Description("The setting whose value you want to change")]
        string setting,
        [Description("Setting value")] string value) {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
            return Result.FromError(
                new ArgumentNullError(nameof(_context), "Unable to retrieve necessary IDs from command context"));

        var currentUserResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!currentUserResult.IsDefined(out var currentUser))
            return Result.FromError(currentUserResult);

        var cfg = await _dataService.GetConfiguration(guildId.Value, CancellationToken);
        Messages.Culture = cfg.GetCulture();

        PropertyInfo? property = null;

        try {
            foreach (var prop in typeof(GuildConfiguration).GetProperties())
                if (string.Equals(setting, prop.Name, StringComparison.CurrentCultureIgnoreCase))
                    property = prop;
            if (property == null || !property.CanWrite)
                throw new ApplicationException(Messages.SettingDoesntExist);
            var type = property.PropertyType;

            if (value is "reset" or "default") { property.SetValue(cfg, null); } else if (type == typeof(string)) {
                if (setting == "language" && value is not ("ru" or "en" or "mctaylors-ru"))
                    throw new ApplicationException(Messages.LanguageNotSupported);
                property.SetValue(cfg, value);
            } else {
                try {
                    if (type == typeof(bool))
                        property.SetValue(cfg, Convert.ToBoolean(value));

                    if (type == typeof(ulong)) {
                        var id = Convert.ToUInt64(value);

                        property.SetValue(cfg, id);
                    }
                } catch (Exception e) when (e is FormatException or OverflowException) {
                    throw new ApplicationException(Messages.InvalidSettingValue);
                }
            }
        } catch (Exception e) {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.SettingNotChanged, currentUser)
                .WithDescription(e.Message)
                .WithColour(ColorsList.Red)
                .Build();
            if (!failedEmbed.IsDefined(out var failedBuilt)) return Result.FromError(failedEmbed);

            return (Result)await _feedbackService.SendContextualEmbedAsync(failedBuilt, ct: CancellationToken);
        }

        var builder = new StringBuilder();

        builder.Append(Markdown.InlineCode(setting))
            .Append($" {Messages.SettingIsNow} ")
            .Append(Markdown.InlineCode(value));

        var embed = new EmbedBuilder().WithSmallTitle(Messages.SettingSuccessfullyChanged, currentUser)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Green)
            .Build();
        if (!embed.IsDefined(out var built)) return Result.FromError(embed);

        return (Result)await _feedbackService.SendContextualEmbedAsync(built, ct: CancellationToken);
    }
}
