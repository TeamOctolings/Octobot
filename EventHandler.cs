using System.Diagnostics;
using Boyfriend.Data;
using Discord;
using Discord.Rest;
using Discord.WebSocket;

namespace Boyfriend;

public static class EventHandler {
    private static readonly DiscordSocketClient Client             = Boyfriend.Client;
    private static          bool                _sendReadyMessages = true;

    public static void InitEvents() {
        Client.Ready += ReadyEvent;
        Client.MessageDeleted += MessageDeletedEvent;
        Client.MessageReceived += MessageReceivedEvent;
        Client.MessageUpdated += MessageUpdatedEvent;
        Client.UserJoined += UserJoinedEvent;
        Client.UserLeft += UserLeftEvent;
        Client.GuildMemberUpdated += MemberRolesUpdatedEvent;
        Client.GuildScheduledEventCreated += ScheduledEventCreatedEvent;
        Client.GuildScheduledEventCancelled += ScheduledEventCancelledEvent;
        Client.GuildScheduledEventStarted += ScheduledEventStartedEvent;
        Client.GuildScheduledEventCompleted += ScheduledEventCompletedEvent;
    }

    private static Task MemberRolesUpdatedEvent(Cacheable<SocketGuildUser, ulong> oldUser, SocketGuildUser newUser) {
        var data = GuildData.Get(newUser.Guild).MemberData[newUser.Id];
        data.Roles = ((IGuildUser)newUser).RoleIds.ToList();
        data.Roles.Remove(newUser.Guild.Id);
        return Task.CompletedTask;
    }

    private static Task ReadyEvent() {
        if (!_sendReadyMessages) return Task.CompletedTask;
        var i = Random.Shared.Next(3);

        foreach (var guild in Client.Guilds) {
            var data = GuildData.Get(guild);
            var config = data.Preferences;
            var channel = data.PrivateFeedbackChannel;
            Utils.SetCurrentLanguage(guild);

            if (config["ReceiveStartupMessages"] is not "true" || channel is null) continue;
            _ = channel.SendMessageAsync(string.Format(Messages.Ready, Utils.GetBeep(i)));
        }

        _sendReadyMessages = false;
        return Task.CompletedTask;
    }

    private static async Task MessageDeletedEvent(
        Cacheable<IMessage, ulong>        message,
        Cacheable<IMessageChannel, ulong> channel) {
        var msg = message.Value;
        if (channel.Value is not SocketGuildChannel gChannel
            || msg is null or ISystemMessage
            || msg.Author.IsBot) return;

        var guild = gChannel.Guild;

        Utils.SetCurrentLanguage(guild);

        var mention = msg.Author.Mention;

        await Task.Delay(500);

        var auditLogEntry = (await guild.GetAuditLogsAsync(1).FlattenAsync()).First();
        if (auditLogEntry.CreatedAt >= DateTimeOffset.Now.Subtract(TimeSpan.FromSeconds(1))
            && auditLogEntry.Data is MessageDeleteAuditLogData data
            && msg.Author.Id == data.Target.Id)
            mention = auditLogEntry.User.Mention;

        await Utils.SendFeedbackAsync(
            string.Format(
                Messages.CachedMessageDeleted, msg.Author.Mention,
                Utils.MentionChannel(channel.Id),
                Utils.Wrap(msg.CleanContent)), guild, mention);
    }

    private static Task MessageReceivedEvent(IDeletable messageParam) {
        if (messageParam is not SocketUserMessage message) return Task.CompletedTask;

        _ = message.CleanContent.ToLower() switch {
            "whoami"  => message.ReplyAsync("`nobody`"),
            "сука !!" => message.ReplyAsync("`root`"),
            "воооо"   => message.ReplyAsync("`removing /...`"),
            "op ??" => message.ReplyAsync(
                "некоторые пасхальные цитаты которые вы могли найти были легально взяты у <@573772175572729876>"),
            "++++" => message.ReplyAsync("#"),
            _ => new CommandProcessor(message).HandleCommandAsync()
        };
        return Task.CompletedTask;
    }

    private static async Task MessageUpdatedEvent(
        Cacheable<IMessage, ulong> messageCached, IMessage messageSocket,
        ISocketMessageChannel      channel) {
        var msg = messageCached.Value;
        if (channel is not SocketGuildChannel gChannel
            || msg is null or ISystemMessage
            || msg.CleanContent == messageSocket.CleanContent
            || msg.Author.IsBot) return;

        var guild = gChannel.Guild;
        Utils.SetCurrentLanguage(guild);

        var isLimitedSpace = msg.CleanContent.Length + messageSocket.CleanContent.Length < 1940;

        await Utils.SendFeedbackAsync(
            string.Format(
                Messages.CachedMessageEdited, Utils.MentionChannel(channel.Id),
                Utils.Wrap(msg.CleanContent, isLimitedSpace), Utils.Wrap(messageSocket.CleanContent, isLimitedSpace)),
            guild, msg.Author.Mention);
    }

    private static async Task UserJoinedEvent(SocketGuildUser user) {
        if (user.IsBot) return;
        var guild = user.Guild;
        var data = GuildData.Get(guild);
        var config = data.Preferences;
        Utils.SetCurrentLanguage(guild);

        if (config["SendWelcomeMessages"] is "true" && data.PublicFeedbackChannel is not null)
            await Utils.SilentSendAsync(
                data.PublicFeedbackChannel,
                string.Format(
                    config["WelcomeMessage"] is "default"
                        ? Messages.DefaultWelcomeMessage
                        : config["WelcomeMessage"], user.Mention, guild.Name));

        if (config["StarterRole"] is not "0") await user.AddRoleAsync(ulong.Parse(config["StarterRole"]));

        if (!data.MemberData.ContainsKey(user.Id)) data.MemberData.Add(user.Id, new MemberData(user));
        var memberData = data.MemberData[user.Id];
        memberData.IsInGuild = true;
        memberData.BannedUntil = null;
        if (memberData.LeftAt.Count > 0) {
            if (memberData.JoinedAt.Contains(user.JoinedAt!.Value))
                throw new UnreachableException();
            memberData.JoinedAt.Add(user.JoinedAt!.Value);
        }

        if (memberData.MutedUntil < DateTimeOffset.Now) {
            if (data.MuteRole is not null && !user.TimedOutUntil.HasValue)
                await user.AddRoleAsync(data.MuteRole);
            if (config["RemoveRolesOnMute"] is "false" && config["ReturnRolesOnRejoin"] is "true")
                await user.AddRolesAsync(memberData.Roles);
        } else if (config["ReturnRolesOnRejoin"] is "true") { await user.AddRolesAsync(memberData.Roles); }
    }

    private static Task UserLeftEvent(SocketGuild guild, SocketUser user) {
        if (user.IsBot) return Task.CompletedTask;
        var data = GuildData.Get(guild).MemberData[user.Id];
        data.IsInGuild = false;
        data.LeftAt.Add(DateTimeOffset.Now);
        return Task.CompletedTask;
    }

    private static async Task ScheduledEventCreatedEvent(SocketGuildEvent scheduledEvent) {
        var guild = scheduledEvent.Guild;
        var eventConfig = GuildData.Get(guild).Preferences;
        var channel = Utils.GetEventNotificationChannel(guild);
        Utils.SetCurrentLanguage(guild);
        if (channel is null) return;

        var role = guild.GetRole(ulong.Parse(eventConfig["EventNotificationRole"]));
        var mentions = role is not null
            ? $"{role.Mention} {scheduledEvent.Creator.Mention}"
            : $"{scheduledEvent.Creator.Mention}";

        var location = Utils.Wrap(scheduledEvent.Location) ?? Utils.MentionChannel(scheduledEvent.Channel.Id);
        var descAndLink
            = $"\n{Utils.Wrap(scheduledEvent.Description)}\nhttps://discord.com/events/{guild.Id}/{scheduledEvent.Id}";

        await Utils.SilentSendAsync(
            channel,
            string.Format(
                Messages.EventCreated, mentions,
                Utils.Wrap(scheduledEvent.Name), location,
                scheduledEvent.StartTime.ToUnixTimeSeconds().ToString(), descAndLink),
            true);
    }

    private static async Task ScheduledEventCancelledEvent(SocketGuildEvent scheduledEvent) {
        var guild = scheduledEvent.Guild;
        var eventConfig = GuildData.Get(guild).Preferences;
        var channel = Utils.GetEventNotificationChannel(guild);
        Utils.SetCurrentLanguage(guild);
        if (channel is not null)
            await channel.SendMessageAsync(
                string.Format(
                    Messages.EventCancelled, Utils.Wrap(scheduledEvent.Name),
                    eventConfig["FrowningFace"] is "true" ? $" {Messages.SettingsFrowningFace}" : ""));
    }

    private static async Task ScheduledEventStartedEvent(SocketGuildEvent scheduledEvent) {
        var guild = scheduledEvent.Guild;
        var eventConfig = GuildData.Get(guild).Preferences;
        var channel = Utils.GetEventNotificationChannel(guild);
        Utils.SetCurrentLanguage(guild);

        if (channel is null) return;

        var receivers = eventConfig["EventStartedReceivers"];
        var role = guild.GetRole(ulong.Parse(eventConfig["EventNotificationRole"]));
        var mentions = Boyfriend.StringBuilder;

        if (receivers.Contains("role") && role is not null) mentions.Append($"{role.Mention} ");
        if (receivers.Contains("users") || receivers.Contains("interested"))
            mentions = (await scheduledEvent.GetUsersAsync(15))
                .Where(user => role is null || !((RestGuildUser)user).RoleIds.Contains(role.Id))
                .Aggregate(mentions, (current, user) => current.Append($"{user.Mention} "));

        await channel.SendMessageAsync(
            string.Format(
                Messages.EventStarted, mentions,
                Utils.Wrap(scheduledEvent.Name),
                Utils.Wrap(scheduledEvent.Location) ?? Utils.MentionChannel(scheduledEvent.Channel.Id)));
        mentions.Clear();
    }

    private static async Task ScheduledEventCompletedEvent(SocketGuildEvent scheduledEvent) {
        var guild = scheduledEvent.Guild;
        var channel = Utils.GetEventNotificationChannel(guild);
        Utils.SetCurrentLanguage(guild);
        if (channel is not null)
            await channel.SendMessageAsync(
                string.Format(
                    Messages.EventCompleted, Utils.Wrap(scheduledEvent.Name),
                    Utils.GetHumanizedTimeSpan(DateTimeOffset.Now.Subtract(scheduledEvent.StartTime))));
    }
}
