using System.Globalization;
using Boyfriend.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend;

public class EventHandler {
    private readonly DiscordSocketClient _client = Boyfriend.Client;

    public void InitEvents() {
        _client.Ready += ReadyEvent;
        _client.MessageDeleted += MessageDeletedEvent;
        _client.MessageReceived += MessageReceivedEvent;
        _client.MessageUpdated += MessageUpdatedEvent;
        _client.UserJoined += UserJoinedEvent;
    }

    private static async Task ReadyEvent() {
        await Boyfriend.SetupGuildConfigs();

        var i = new Random().Next(3);

        foreach (var guild in Boyfriend.Client.Guilds) {
            var config = Boyfriend.GetGuildConfig(guild);
            var channel = guild.GetTextChannel(config.BotLogChannel.GetValueOrDefault(0));
            Messages.Culture = new CultureInfo(config.Lang!);

            if (!config.ReceiveStartupMessages.GetValueOrDefault(true) || channel == null) continue;
            await channel.SendMessageAsync(string.Format(Messages.Ready, Utils.GetBeep(config.Lang!, i)));
        }
    }

    private static async Task MessageDeletedEvent(Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel) {
        var msg = message.Value;

        var toSend = msg == null
            ? string.Format(Messages.UncachedMessageDeleted, Utils.MentionChannel(channel.Id))
            : string.Format(Messages.CachedMessageDeleted, msg.Author.Mention) +
              $"{Utils.MentionChannel(channel.Id)}: {Environment.NewLine}{Utils.Wrap(msg.Content)}";
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(
            Boyfriend.FindGuild(channel.Value)), toSend);
    }

    private static async Task MessageReceivedEvent(SocketMessage messageParam) {
        if (messageParam is not SocketUserMessage message) return;

        var argPos = 0;
        var user = (IGuildUser) message.Author;
        var guild = user.Guild;
        var guildConfig = Boyfriend.GetGuildConfig(guild);
        var prev = "";
        var prevFailsafe = "";
        var prevs = await message.Channel.GetMessagesAsync(3).FlattenAsync();
        var prevsArray = prevs as IMessage[] ?? prevs.ToArray();

        Messages.Culture = new CultureInfo(guildConfig.Lang!);

        if ((message.MentionedUsers.Count > 3 || message.MentionedRoles.Count > 2)
            && !user.GuildPermissions.MentionEveryone)
            await BanCommand.BanUser(guild, null, await guild.GetCurrentUserAsync(), user,
                TimeSpan.FromMilliseconds(-1), Messages.AutobanReason);

        try {
            prev = prevsArray[1].Content;
            prevFailsafe = prevsArray[2].Content;
        }
        catch (IndexOutOfRangeException) { }

        if (!(message.HasStringPrefix(guildConfig.Prefix, ref argPos)
              || message.HasMentionPrefix(Boyfriend.Client.CurrentUser, ref argPos))
            || user == await guild.GetCurrentUserAsync()
            || user.IsBot && (message.Content.Contains(prev) || message.Content.Contains(prevFailsafe)))
            return;

        await CommandHandler.HandleCommand(message);
    }

    private static async Task MessageUpdatedEvent(Cacheable<IMessage, ulong> messageCached, SocketMessage messageSocket,
        ISocketMessageChannel channel) {
        var msg = messageCached.Value;
        var nl = Environment.NewLine;

        if (msg != null && msg.Content == messageSocket.Content) return;

        var toSend = msg == null
            ? string.Format(Messages.UncachedMessageEdited, messageSocket.Author.Mention,
                  Utils.MentionChannel(channel.Id)) +
              Utils.Wrap(messageSocket.Content)
            : string.Format(Messages.CachedMessageEdited, msg.Author.Mention, Utils.MentionChannel(channel.Id), nl, nl,
                Utils.Wrap(msg.Content), nl, nl, Utils.Wrap(messageSocket.Content));
        await Utils.SilentSendAsync(await Utils.GetAdminLogChannel(Boyfriend.FindGuild(channel)),
                toSend);
    }

    private static async Task UserJoinedEvent(SocketGuildUser user) {
        var guild = user.Guild;
        var config = Boyfriend.GetGuildConfig(guild);

        if (config.SendWelcomeMessages.GetValueOrDefault(true))
            await Utils.SilentSendAsync(guild.SystemChannel, string.Format(config.WelcomeMessage!, user.Mention,
                guild.Name));

        if (config.DefaultRole != 0)
            await user.AddRoleAsync(Utils.ParseRole(guild, config.DefaultRole.ToString()!));
    }
}
