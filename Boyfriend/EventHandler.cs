using System.Reflection;
using Boyfriend.Commands;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend;

public class EventHandler {
    private readonly DiscordSocketClient _client = Boyfriend.Client;
    private readonly CommandService _commands = new();

    public async Task InitEvents() {
        _client.Ready += ReadyEvent;
        _client.MessageDeleted += MessageDeletedEvent;
        _client.MessageReceived += MessageReceivedEvent;
        _client.MessageUpdated += MessageUpdatedEvent;
        _client.UserJoined += UserJoinedEvent;
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
    }

    [Obsolete("Stop hard-coding things!")]
    private async Task ReadyEvent() {
        if (_client.GetChannel(618044439939645444) is not IMessageChannel botLogChannel)
            throw new ArgumentException("Invalid bot log channel");
        await botLogChannel.SendMessageAsync(Utils.GetBeep() +
            "Я запустился! (C#)");
    }

    private static async Task MessageDeletedEvent(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel) {
        var msg = message.Value;
        string toSend;
        if (msg == null)
            toSend = "Удалено сообщение в канале " + Utils.MentionChannel(channel.Id) + ", но я забыл что там было";
        else
            toSend = "Удалено сообщение от " + msg.Author.Mention + " в канале " + Utils.MentionChannel(channel.Id)
                     + ": " + Utils.Wrap(msg.Content);
        await Utils.GetAdminLogChannel().SendMessageAsync(toSend);
    }

    private async Task MessageReceivedEvent(SocketMessage messageParam) {
        if (messageParam is not SocketUserMessage {Author: IGuildUser user} message) return;
        var argPos = 0;
        var guild = user.Guild;

        if ((message.MentionedUsers.Count > 3 || message.MentionedRoles.Count > 2)
            && !user.GuildPermissions.MentionEveryone)
            await new BanModule().BanUser(guild, guild.GetCurrentUserAsync().Result, user, TimeSpan.Zero,
                "Более 3-ёх упоминаний в одном сообщении");

        if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
            message.Author.IsBot)
            return;

        var context = new SocketCommandContext(_client, message);

        await _commands.ExecuteAsync(context, argPos, null);
    }

    private static async Task MessageUpdatedEvent(Cacheable<IMessage, ulong> messageCached, SocketMessage messageSocket,
        ISocketMessageChannel channel) {
        var msg = messageCached.Value;
        string toSend;
        if (msg == null)
            toSend = "Отредактировано сообщение в канале "
                     + Utils.MentionChannel(channel.Id) + ", но я забыл что там было до редактирования: "
                     + Utils.Wrap(messageSocket.Content);
        else
            toSend = "Отредактировано сообщение от " + msg.Author.Mention + " в канале "
                     + Utils.MentionChannel(channel.Id) + ": " + Utils.Wrap(msg.Content)
                     + Utils.Wrap(messageSocket.Content);
        await Utils.GetAdminLogChannel().SendMessageAsync(toSend);
    }

    private static async Task UserJoinedEvent(SocketGuildUser user) {
        await user.Guild.SystemChannel.SendMessageAsync(user.Mention + ", добро пожаловать на сервер "
                                                                     + user.Guild.Name);
    }
}