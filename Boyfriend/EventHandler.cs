using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Humanizer;

namespace Boyfriend;

public static class EventHandler {
    private static readonly DiscordSocketClient Client = Boyfriend.Client;
    private static bool _sendReadyMessages = true;

    public static void InitEvents() {
        Client.Ready += ReadyEvent;
        Client.MessageDeleted += MessageDeletedEvent;
        Client.MessageReceived += MessageReceivedEvent;
        Client.MessageUpdated += MessageUpdatedEvent;
        Client.UserJoined += UserJoinedEvent;
        Client.GuildScheduledEventCreated += ScheduledEventCreatedEvent;
        Client.GuildScheduledEventCancelled += ScheduledEventCancelledEvent;
        Client.GuildScheduledEventStarted += ScheduledEventStartedEvent;
        Client.GuildScheduledEventCompleted += ScheduledEventCompletedEvent;
    }

    private static Task ReadyEvent() {
        if (!_sendReadyMessages) return Task.CompletedTask;
        var i = Utils.Random.Next(3);

        foreach (var guild in Client.Guilds) {
            var config = Boyfriend.GetGuildConfig(guild.Id);
            var channel = guild.GetTextChannel(Convert.ToUInt64(config["BotLogChannel"]));
            Utils.SetCurrentLanguage(guild.Id);

            if (config["ReceiveStartupMessages"] is not "true" || channel == null) continue;
            _ = channel.SendMessageAsync(string.Format(Messages.Ready, Utils.GetBeep(i)));
        }

        _sendReadyMessages = false;
        return Task.CompletedTask;
    }

    private static async Task MessageDeletedEvent(Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel) {
        var msg = message.Value;
        if (msg is null or ISystemMessage || msg.Author.IsBot) return;

        var guild = Boyfriend.FindGuild(channel.Value.Id);

        Utils.SetCurrentLanguage(guild.Id);

        var mention = msg.Author.Mention;

        await Task.Delay(500);

        var auditLogEntry = (await guild.GetAuditLogsAsync(1).FlattenAsync()).First();
        if (auditLogEntry.Data is MessageDeleteAuditLogData data && msg.Author.Id == data.Target.Id)
            mention = auditLogEntry.User.Mention;

        await Utils.SendFeedbackAsync(string.Format(Messages.CachedMessageDeleted, msg.Author.Mention,
            Utils.MentionChannel(channel.Id),
            Utils.Wrap(msg.CleanContent)), guild.Id, mention);
    }

    private static Task MessageReceivedEvent(SocketMessage messageParam) {
        if (messageParam is not SocketUserMessage { Author: SocketGuildUser user } message) return Task.CompletedTask;

        var guild = user.Guild;

        Utils.SetCurrentLanguage(guild.Id);

        _ = message.CleanContent.ToLower() switch {
            "whoami" => message.ReplyAsync("`nobody`"),
            "сука !!" => message.ReplyAsync("`root`"),
            "воооо" => message.ReplyAsync("`removing /...`"),
            "op ??" => message.ReplyAsync(
                "некоторые пасхальные цитаты которые вы могли найти были легально взяты у <@573772175572729876>"),
            _ => new CommandProcessor(message).HandleCommandAsync()
        };
        return Task.CompletedTask;
    }

    private static async Task MessageUpdatedEvent(Cacheable<IMessage, ulong> messageCached, SocketMessage messageSocket,
        ISocketMessageChannel channel) {
        var msg = messageCached.Value;

        if (msg is null or ISystemMessage || msg.CleanContent == messageSocket.CleanContent || msg.Author.IsBot) return;

        var guild = Boyfriend.FindGuild(channel.Id);

        Utils.SetCurrentLanguage(guild.Id);

        var isLimitedSpace = msg.CleanContent.Length + messageSocket.CleanContent.Length < 1940;

        await Utils.SendFeedbackAsync(string.Format(Messages.CachedMessageEdited, Utils.MentionChannel(channel.Id),
                Utils.Wrap(msg.CleanContent, isLimitedSpace), Utils.Wrap(messageSocket.CleanContent, isLimitedSpace)),
            guild.Id, msg.Author.Mention);
    }

    private static async Task UserJoinedEvent(SocketGuildUser user) {
        var guild = user.Guild;
        var config = Boyfriend.GetGuildConfig(guild.Id);

        if (config["SendWelcomeMessages"] is "true")
            await Utils.SilentSendAsync(guild.SystemChannel,
                string.Format(config["WelcomeMessage"], user.Mention, guild.Name));

        if (config["StarterRole"] is not "0") await user.AddRoleAsync(ulong.Parse(config["StarterRole"]));
    }

    private static async Task ScheduledEventCreatedEvent(SocketGuildEvent scheduledEvent) {
        var guild = scheduledEvent.Guild;
        var eventConfig = Boyfriend.GetGuildConfig(guild.Id);
        var channel = guild.GetTextChannel(Convert.ToUInt64(eventConfig["EventCreatedChannel"]));

        if (channel != null) {
            var roleMention = "";
            var role = guild.GetRole(Convert.ToUInt64(eventConfig["EventNotifyReceiverRole"]));
            if (role != null) roleMention = $"{role.Mention} ";

            var location = Utils.Wrap(scheduledEvent.Location) ?? Utils.MentionChannel(scheduledEvent.Channel.Id);

            await Utils.SilentSendAsync(channel,
                string.Format(Messages.EventCreated, "\n", roleMention, scheduledEvent.Creator.Mention,
                    Utils.Wrap(scheduledEvent.Name), location,
                    scheduledEvent.StartTime.ToUnixTimeSeconds().ToString(), Utils.Wrap(scheduledEvent.Description),
                    guild.Id, scheduledEvent.Id),
                true);
        }

        if (eventConfig["EventEarlyNotificationOffset"] != "0") {
            _ = Utils.SendEarlyEventStartNotificationAsync(channel, scheduledEvent,
                Convert.ToInt32(eventConfig["EventEarlyNotificationOffset"]));
        }
    }

    private static async Task ScheduledEventCancelledEvent(SocketGuildEvent scheduledEvent) {
        var guild = scheduledEvent.Guild;
        var eventConfig = Boyfriend.GetGuildConfig(guild.Id);
        var channel = guild.GetTextChannel(Convert.ToUInt64(eventConfig["EventCancelledChannel"]));
        if (channel != null)
            await channel.SendMessageAsync(string.Format(Messages.EventCancelled, Utils.Wrap(scheduledEvent.Name),
                eventConfig["FrowningFace"] is "true" ? $" {Messages.SettingsFrowningFace}" : ""));
    }

    private static async Task ScheduledEventStartedEvent(SocketGuildEvent scheduledEvent) {
        var guild = scheduledEvent.Guild;
        var eventConfig = Boyfriend.GetGuildConfig(guild.Id);
        var channel = guild.GetTextChannel(Convert.ToUInt64(eventConfig["EventStartedChannel"]));

        if (channel != null) {
            var receivers = eventConfig["EventStartedReceivers"];
            var role = guild.GetRole(Convert.ToUInt64(eventConfig["EventNotifyReceiverRole"]));
            var mentions = Boyfriend.StringBuilder;

            if (receivers.Contains("role") && role != null) mentions.Append($"{role.Mention} ");
            if (receivers.Contains("users") || receivers.Contains("interested"))
                mentions = (await scheduledEvent.GetUsersAsync(15)).Aggregate(mentions,
                    (current, user) => current.Append($"{user.Mention} "));

            await channel.SendMessageAsync(string.Format(Messages.EventStarted, mentions,
                Utils.Wrap(scheduledEvent.Name),
                Utils.Wrap(scheduledEvent.Location) ?? Utils.MentionChannel(scheduledEvent.Channel.Id)));
            mentions.Clear();
        }
    }

    private static async Task ScheduledEventCompletedEvent(SocketGuildEvent scheduledEvent) {
        var guild = scheduledEvent.Guild;
        var eventConfig = Boyfriend.GetGuildConfig(guild.Id);
        var channel = guild.GetTextChannel(Convert.ToUInt64(eventConfig["EventCompletedChannel"]));
        if (channel != null)
            await channel.SendMessageAsync(string.Format(Messages.EventCompleted, Utils.Wrap(scheduledEvent.Name),
                Utils.Wrap(scheduledEvent.StartTime.Subtract(DateTimeOffset.Now).Negate().Humanize())));
    }
}
