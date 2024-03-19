using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using JetBrains.Annotations;
using Octobot.Data;
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
using Remora.Rest.Core;
using Remora.Results;

namespace Octobot.Commands;

[UsedImplicitly]
public class WarnCommandGroup : CommandGroup
{
    private readonly IDiscordRestChannelAPI _channelApi;
    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly IDiscordRestGuildAPI _guildApi;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;
    private readonly Utility _utility;

    public WarnCommandGroup(
        ICommandContext context, IDiscordRestChannelAPI channelApi, GuildDataService guildData,
        IFeedbackService feedback, IDiscordRestGuildAPI guildApi, IDiscordRestUserAPI userApi,
        Utility utility)
    {
        _context = context;
        _channelApi = channelApi;
        _guildData = guildData;
        _feedback = feedback;
        _guildApi = guildApi;
        _userApi = userApi;
        _utility = utility;
    }

    [Command("warn")]
    [DiscordDefaultMemberPermissions(DiscordPermission.KickMembers)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.KickMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.KickMembers)]
    [Description("Warn user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteWarnAsync(
        [Description("User to warn")] IUser target,
        [Description("Warn reason")] [MaxLength(256)]
        string reason)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return Result.FromError(guildResult);
        }

        var data = await _guildData.GetData(guild.ID, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await WarnUserAsync(executor, target, reason, guild, data, channelId, bot, CancellationToken);
    }

    private async Task<Result> WarnUserAsync(IUser executor, IUser target, string reason, IGuild guild,
        GuildData data, Snowflake channelId, IUser bot, CancellationToken ct = default)
    {
        var memberData = data.GetOrCreateMemberData(target.ID);
        memberData.Warns++;

        var warnsThreshold = GuildSettings.WarnsThreshold.Get(data.Settings);

        var builder = new StringBuilder()
            .AppendBulletPointLine(string.Format(Messages.DescriptionActionReason, reason))
            .AppendBulletPointLine(string.Format(Messages.DescriptionActionWarns,
                warnsThreshold is 0 ? memberData.Warns : $"{memberData.Warns}/{warnsThreshold}"));

        var title = string.Format(Messages.UserWarned, target.GetTag());
        var description = builder.ToString();

        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, ct);
        if (dmChannelResult.IsDefined(out var dmChannel))
        {
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YouHaveBeenWarned)
                .WithDescription(description)
                .WithActionFooter(executor)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Yellow)
                .Build();

            await _channelApi.CreateMessageWithEmbedResultAsync(dmChannel.ID, embedResult: dmEmbed, ct: ct);
        }

        _utility.LogAction(
            data.Settings, channelId, executor, title, description, target, ColorsList.Yellow, false, ct);

        if (memberData.Warns >= warnsThreshold &&
            GuildSettings.WarnPunishment.Get(data.Settings) is not "off" and not "disable" and not "disabled")
        {
            memberData.Warns = 0;
            return await PunishUserAsync(target, guild, data, channelId, bot, CancellationToken);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                title, target)
            .WithColour(ColorsList.Green).Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    private async Task<Result> PunishUserAsync(IUser target, IGuild guild,
        GuildData data, Snowflake channelId, IUser bot, CancellationToken ct)
    {
        var settings = data.Settings;
        var duration = GuildSettings.WarnPunishmentDuration.Get(settings);

        if (GuildSettings.WarnPunishment.Get(settings) is "ban"
            && duration != TimeSpan.Zero)
        {
            var banCommandGroup = new BanCommandGroup(_context, _channelApi, _guildData, _feedback, _guildApi, _userApi, _utility);
            await banCommandGroup.BanUserAsync(bot, target, Messages.ReceivedTooManyWarnings,
                duration, guild, data, channelId, bot, ct);
        }

        if (GuildSettings.WarnPunishment.Get(settings) is "kick")
        {
            var kickCommandGroup = new KickCommandGroup(_context, _channelApi, _guildData, _feedback, _guildApi, _userApi, _utility);
            await kickCommandGroup.KickUserAsync(bot, target, Messages.ReceivedTooManyWarnings,
                guild, channelId, data, bot, ct);
        }

        if (GuildSettings.WarnPunishment.Get(settings) is "mute"
            && duration != TimeSpan.Zero)
        {
            var muteCommandGroup = new MuteCommandGroup(_context, _guildData, _feedback, _guildApi, _userApi, _utility);
            await muteCommandGroup.MuteUserAsync(bot, target, Messages.ReceivedTooManyWarnings,
                duration, guild.ID, data, channelId, bot, ct);
        }

        return Result.FromSuccess();
    }

    [Command("unwarn")]
    [DiscordDefaultMemberPermissions(DiscordPermission.KickMembers)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.KickMembers)]
    [RequireBotDiscordPermissions(DiscordPermission.KickMembers)]
    [Description("Remove warns from user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteUnwarnAsync(
        [Description("User to remove warns from")]
        IUser target,
        [Description("Warns remove reason")] [MaxLength(256)]
        string reason)
    {
        if (!_context.TryGetContextIDs(out var guildId, out var channelId, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return Result.FromError(botResult);
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return Result.FromError(executorResult);
        }

        var guildResult = await _guildApi.GetGuildAsync(guildId, ct: CancellationToken);
        if (!guildResult.IsDefined(out var guild))
        {
            return Result.FromError(guildResult);
        }

        var data = await _guildData.GetData(guild.ID, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await RemoveUserWarnsAsync(executor, target, reason, guild, data, channelId, bot, CancellationToken);
    }

    private async Task<Result> RemoveUserWarnsAsync(IUser executor, IUser target, string reason, IGuild guild,
        GuildData data, Snowflake channelId, IUser bot, CancellationToken ct = default)
    {
        var memberData = data.GetOrCreateMemberData(target.ID);
        if (memberData.Warns is 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserHasNoWarnings, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        memberData.Warns = 0;

        var builder = new StringBuilder().AppendBulletPointLine(string.Format(Messages.DescriptionActionReason, reason));

        var title = string.Format(Messages.UserWarnsRemoved, target.GetTag());
        var description = builder.ToString();

        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, ct);
        if (dmChannelResult.IsDefined(out var dmChannel))
        {
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YourWarningsHaveBeenRevoked)
                .WithDescription(description)
                .WithActionFooter(executor)
                .WithCurrentTimestamp()
                .WithColour(ColorsList.Green)
                .Build();

            await _channelApi.CreateMessageWithEmbedResultAsync(dmChannel.ID, embedResult: dmEmbed, ct: ct);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                title, target)
            .WithColour(ColorsList.Green).Build();

        _utility.LogAction(
            data.Settings, channelId, executor, title, description, target, ColorsList.Yellow, false, ct);

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
