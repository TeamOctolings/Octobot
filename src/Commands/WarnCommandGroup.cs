using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Nodes;
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
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;
using static System.DateTimeOffset;

namespace Octobot.Commands;

[UsedImplicitly]
public class WarnCommandGroup : CommandGroup
{
    private readonly AccessControlService _access;
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
        Utility utility, AccessControlService access)
    {
        _context = context;
        _channelApi = channelApi;
        _guildData = guildData;
        _feedback = feedback;
        _guildApi = guildApi;
        _userApi = userApi;
        _utility = utility;
        _access = access;
    }

    [Command("warn")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageMessages)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageMessages)]
    [RequireBotDiscordPermissions(DiscordPermission.KickMembers,
        DiscordPermission.ModerateMembers, DiscordPermission.BanMembers)]
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

        var interactionResult
            = await _access.CheckInteractionsAsync(guild.ID, executor.ID, target.ID, "Warn", CancellationToken);
        if (!interactionResult.IsSuccess)
        {
            return ResultExtensions.FromError(interactionResult);
        }

        if (interactionResult.Entity is not null)
        {
            var errorEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(errorEmbed, ct: CancellationToken);
        }

        return await WarnPreparationAsync(executor, target, reason, guild, data, channelId, bot, CancellationToken);
    }

    private async Task<Result> WarnPreparationAsync(IUser executor, IUser target, string reason, IGuild guild,
        GuildData data, Snowflake channelId, IUser bot, CancellationToken ct = default)
    {
        var memberData = data.GetOrCreateMemberData(target.ID);
        var warns = memberData.Warns;

        var settings = data.Settings;

        var warnThreshold = GuildSettings.WarnsThreshold.Get(settings);
        var warnPunishment = GuildSettings.WarnPunishment.Get(settings);
        var warnDuration = GuildSettings.WarnPunishmentDuration.Get(settings);

        if (warnPunishment is "off" or "disable" or "disabled"
            && warns.Count + 1 >= warnThreshold
            && warnThreshold is not 0)
        {
            var errorEmbed = new EmbedBuilder()
                .WithSmallTitle(string.Format(Messages.WarnThresholdExceeded, warnThreshold), bot)
                .WithDescription(Messages.WarnThresholdExceededDescription)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(errorEmbed, ct: CancellationToken);
        }

        if (warnPunishment is "ban" or "mute" && warnDuration == TimeSpan.Zero)
        {
            var errorEmbed = new EmbedBuilder()
                .WithSmallTitle(Messages.WarnPunishmentDurationNotSet, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(errorEmbed, ct: CancellationToken);
        }

        return await WarnUserAsync(executor, target, reason, guild, data, channelId, bot, settings,
            warns, warnThreshold, warnPunishment, warnDuration, ct);
    }

    private async Task<Result> WarnUserAsync(IUser executor, IUser target, string reason, IGuild guild,
        GuildData data, Snowflake channelId, IUser bot, JsonNode settings, IList warns, int warnThreshold,
        string warnPunishment, TimeSpan warnDuration, CancellationToken ct = default)
    {
        warns.Add(new Warn
        {
            WarnedBy = executor.ID.Value,
            At = UtcNow,
            Reason = reason
        });

        var builder = new StringBuilder()
            .AppendBulletPointLine(string.Format(Messages.DescriptionActionReason, reason))
            .AppendBulletPointLine(string.Format(Messages.DescriptionWarns,
                warnThreshold is 0 ? warns.Count : $"{warns.Count}/{warnThreshold}"));

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

        _utility.LogAction(settings, channelId, executor, title, description,
            target, ColorsList.Yellow, false, ct);

        var embed = new EmbedBuilder().WithSmallTitle(
                title, target)
            .WithColour(ColorsList.Green).Build();

        if (warns.Count >= warnThreshold && warnThreshold is not 0)
        {
            return await PunishUserAsync(target, guild, data, channelId, bot, warns, warnPunishment, warnDuration, CancellationToken);
        }

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    private async Task<Result> PunishUserAsync(IUser target, IGuild guild, GuildData data,
        Snowflake channelId, IUser bot, IList warns, string punishment, TimeSpan duration, CancellationToken ct)
    {
        if (punishment is "ban" && duration != TimeSpan.Zero)
        {
            var banCommandGroup = new BanCommandGroup(
                _access, _channelApi, _context, _feedback, _guildApi, _guildData, _userApi, _utility);
            await banCommandGroup.BanUserAsync(bot, target, Messages.ReceivedTooManyWarnings,
                duration, guild, data, channelId, bot, ct);
        }

        if (punishment is "kick")
        {
            var kickCommandGroup = new KickCommandGroup(
                _access, _channelApi, _context, _feedback, _guildApi, _guildData, _userApi, _utility);
            await kickCommandGroup.KickUserAsync(bot, target, Messages.ReceivedTooManyWarnings,
                guild, channelId, data, bot, ct);
        }

        if (punishment is "mute" && duration != TimeSpan.Zero)
        {
            var muteCommandGroup = new MuteCommandGroup(
                _access, _context, _feedback, _guildApi, _guildData, _userApi, _utility);
            await muteCommandGroup.MuteUserAsync(bot, target, Messages.ReceivedTooManyWarnings,
                duration, guild.ID, data, channelId, bot, ct);
        }

        warns.Clear();
        return Result.FromSuccess();
    }

    [Command("unwarn")]
    [DiscordDefaultMemberPermissions(DiscordPermission.ManageMessages)]
    [DiscordDefaultDMPermission(false)]
    [RequireContext(ChannelContext.Guild)]
    [RequireDiscordPermission(DiscordPermission.ManageMessages)]
    [Description("Remove warns from user")]
    [UsedImplicitly]
    public async Task<Result> ExecuteUnwarnAsync(
        [Description("User to remove warns from")]
        IUser target,
        [Description("Warn remove reason")] [MaxLength(256)]
        string reason,
        [Description("Number of the warning to be deleted")]
        int? number = null)
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

        var interactionResult
            = await _access.CheckInteractionsAsync(guild.ID, executor.ID, target.ID, "Unwarn", CancellationToken);
        if (!interactionResult.IsSuccess)
        {
            return ResultExtensions.FromError(interactionResult);
        }

        if (interactionResult.Entity is not null)
        {
            var errorEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(errorEmbed, ct: CancellationToken);
        }

        if (number is not null)
        {
            return await RemoveUserWarnAsync(executor, target, reason, number.Value, guild, data, channelId, bot,
                CancellationToken);
        }

        return await RemoveUserWarnsAsync(executor, target, reason, guild, data, channelId, bot, CancellationToken);
    }

    private async Task<Result> RemoveUserWarnAsync(IUser executor, IUser target, string reason, int warnNumber,
        IGuild guild, GuildData data, Snowflake channelId, IUser bot, CancellationToken ct = default)
    {
        var memberData = data.GetOrCreateMemberData(target.ID);
        var warns = memberData.Warns;

        if (warns.Count is 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserHasNoWarnings, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        var index = warnNumber - 1;

        if (index >= warns.Count || index < 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.WrongWarningNumberSelected, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        var builder = new StringBuilder()
            .Append("> ").AppendLine(warns[index].Reason)
            .AppendBulletPointLine(string.Format(Messages.DescriptionActionReason, reason));

        warns.RemoveAt(index);

        var title = string.Format(Messages.UserWarnRemoved, warnNumber, target.GetTag());
        var description = builder.ToString();

        var dmChannelResult = await _userApi.CreateDMAsync(target.ID, ct);
        if (dmChannelResult.IsDefined(out var dmChannel))
        {
            var dmEmbed = new EmbedBuilder().WithGuildTitle(guild)
                .WithTitle(Messages.YourWarningHasBeenRevoked)
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

    private async Task<Result> RemoveUserWarnsAsync(IUser executor, IUser target, string reason,
        IGuild guild, GuildData data, Snowflake channelId, IUser bot, CancellationToken ct = default)
    {
        var memberData = data.GetOrCreateMemberData(target.ID);
        var warns = memberData.Warns;

        if (warns.Count is 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserHasNoWarnings, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        var builder = new StringBuilder().AppendBulletPointLine(string.Format(Messages.DescriptionActionReason, reason));

        warns.Clear();

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

    [Command("listwarn")]
    [DiscordDefaultDMPermission(false)]
    [Ephemeral]
    [Description("(Ephemeral) Get current warns")]
    [UsedImplicitly]
    public async Task<Result> ExecuteListWarnsAsync(
        [Description("(Moderator-only) Get target's current warns")]
        IUser? target = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
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

        if (target is not null)
        {
            return await ListTargetWarnsAsync(executor, target, guild, data, bot, CancellationToken);
        }

        return await ListExecutorWarnsAsync(executor, data, bot, CancellationToken);
    }

    private async Task<Result> ListTargetWarnsAsync(IUser executor, IUser target, IGuild guild,
        GuildData data, IUser bot, CancellationToken ct = default)
    {
        var interactionResult
            = await _access.CheckInteractionsAsync(guild.ID, executor.ID, target.ID, "GetWarns", ct);
        if (!interactionResult.IsSuccess)
        {
            return ResultExtensions.FromError(interactionResult);
        }

        if (interactionResult.Entity is not null)
        {
            var errorEmbed = new EmbedBuilder().WithSmallTitle(interactionResult.Entity, bot)
                .WithColour(ColorsList.Red).Build();

            return await _feedback.SendContextualEmbedResultAsync(errorEmbed, ct: ct);
        }

        var memberData = data.GetOrCreateMemberData(target.ID);
        var warns = memberData.Warns;

        if (warns.Count is 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.UserHasNoWarnings, bot)
                .WithColour(ColorsList.Green).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        var warnThreshold = GuildSettings.WarnsThreshold.Get(data.Settings);

        var punishmentType = GuildSettings.WarnPunishment.Get(data.Settings);

        var description = new StringBuilder()
            .AppendLine(string.Format(Messages.DescriptionWarns,
                warnThreshold is 0 ? warns.Count : $"{warns.Count}/{warnThreshold}"));
        if (punishmentType is not "off" and not "disable" and not "disabled")
        {
            description.AppendLine(string.Format(
                Messages.DescriptionPunishmentType, Markdown.InlineCode(punishmentType)));
        }

        var warnCount = 0;
        foreach (var warn in warns)
        {
            warnCount++;
            description.Append(warnCount).Append(". ").AppendLine(warn.Reason)
                .AppendSubBulletPoint(Messages.IssuedBy).Append(' ').AppendLine(Mention.User(warn.WarnedBy.ToSnowflake()))
                .AppendSubBulletPointLine(string.Format(Messages.ReceivedOn, Markdown.Timestamp(warn.At)));
        }

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.ListTargetWarnsTitle, target.GetTag()), target)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Default).Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    private async Task<Result> ListExecutorWarnsAsync(IUser executor, GuildData data, IUser bot,
        CancellationToken ct = default)
    {
        var memberData = data.GetOrCreateMemberData(executor.ID);
        var warns = memberData.Warns;

        if (warns.Count is 0)
        {
            var failedEmbed = new EmbedBuilder().WithSmallTitle(Messages.YouHaveNoWarnings, bot)
                .WithColour(ColorsList.Green).Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: ct);
        }

        var warnThreshold = GuildSettings.WarnsThreshold.Get(data.Settings);

        var punishmentType = GuildSettings.WarnPunishment.Get(data.Settings);

        var description = new StringBuilder()
            .AppendLine(string.Format(Messages.DescriptionWarns,
                warnThreshold is 0 ? warns.Count : $"{warns.Count}/{warnThreshold}"));
        if (punishmentType is not "off" and not "disable" and not "disabled")
        {
            description.AppendLine(string.Format(
                Messages.DescriptionPunishmentType, Markdown.InlineCode(punishmentType)));
        }

        var warnCount = 0;
        foreach (var warn in warns)
        {
            warnCount++;
            description.Append(warnCount).Append(". ").AppendLine(warn.Reason)
                .AppendSubBulletPoint(Messages.IssuedBy).Append(' ').AppendLine(Mention.User(warn.WarnedBy.ToSnowflake()))
                .AppendSubBulletPointLine(string.Format(Messages.ReceivedOn, Markdown.Timestamp(warn.At)));
        }

        var embed = new EmbedBuilder()
            .WithSmallTitle(string.Format(Messages.ListExecutorWarnsTitle, executor.GetTag()), executor)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Default).Build();

        return await _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
