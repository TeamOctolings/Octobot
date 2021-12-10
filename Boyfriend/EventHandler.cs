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

    private static async Task HandleErrors(SocketCommandContext context, IResult result) {
        var channel = context.Channel;
        var reason = Utils.WrapInline(result.ErrorReason);
        switch (result.Error) {
            case CommandError.Exception:
                await channel.SendMessageAsync($"Произошла непредвиденная ошибка при выполнении команды: {reason}");
                break;
            case CommandError.Unsuccessful:
                await channel.SendMessageAsync($"Выполнение команды завершилось неудачей: {reason}");
                break;
            case CommandError.MultipleMatches:
                await channel.SendMessageAsync($"Обнаружены повторяющиеся типы аргументов! {reason}");
                break;
            case CommandError.ParseFailed:
                await channel.SendMessageAsync($"Не удалось обработать команду: {reason}");
                break;
            case CommandError.UnknownCommand:
                await channel.SendMessageAsync($"Неизвестная команда! {reason}");
                break;
            case CommandError.UnmetPrecondition:
                await channel.SendMessageAsync($"У тебя недостаточно прав для выполнения этой команды! {reason}");
                break;
            case CommandError.BadArgCount:
                await channel.SendMessageAsync($"Неверное количество аргументов! {reason}");
                break;
            case CommandError.ObjectNotFound:
                await channel.SendMessageAsync($"Нету нужных аргументов! {reason}");
                break;
            case null:
                break;
            default:
                throw new ArgumentException("CommandError");

        }
    }

    [Obsolete("Stop hard-coding things!")]
    private async Task ReadyEvent() {
        if (_client.GetChannel(618044439939645444) is not IMessageChannel botLogChannel)
            throw new ArgumentException("Invalid bot log channel");
        await botLogChannel.SendMessageAsync($"{Utils.GetBeep()}Я запустился! (C#)");
    }

    private static async Task MessageDeletedEvent(Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel) {
        var msg = message.Value;
        var toSend = msg == null
            ? "Удалено сообщение в канале {Utils.MentionChannel(channel.Id)}, но я забыл что там было"
            : $"Удалено сообщение от {msg.Author.Mention} в канале " +
              $"{Utils.MentionChannel(channel.Id)}: {Environment.NewLine}{Utils.Wrap(msg.Content)}";
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), toSend);
    }

    private async Task MessageReceivedEvent(SocketMessage messageParam) {
        if (messageParam is not SocketUserMessage {Author: IGuildUser user} message) return;
        var argPos = 0;
        var guild = user.Guild;

        if ((message.MentionedUsers.Count > 3 || message.MentionedRoles.Count > 2)
            && !user.GuildPermissions.MentionEveryone)
            BanModule.BanUser(guild, guild.GetCurrentUserAsync().Result, user, TimeSpan.FromMilliseconds(-1),
                "Более 3-ёх упоминаний в одном сообщении");

        if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
            message.Author.IsBot)
            return;

        var context = new SocketCommandContext(_client, message);

        var result = await _commands.ExecuteAsync(context, argPos, null);
        await HandleErrors(context, result);
    }

    private static async Task MessageUpdatedEvent(Cacheable<IMessage, ulong> messageCached, SocketMessage messageSocket,
        ISocketMessageChannel channel) {
        var msg = messageCached.Value;
        var nl = Environment.NewLine;
        var toSend = msg == null
            ? $"Отредактировано сообщение от {messageSocket.Author.Mention} в канале" +
              $" {Utils.MentionChannel(channel.Id)}," + $" но я забыл что там было до редактирования: " +
              $"{Utils.Wrap(messageSocket.Content)}"
            : $"Отредактировано сообщение от {msg.Author.Mention} " +
              $"в канале {Utils.MentionChannel(channel.Id)}." +
              $"{nl}До:{nl}{Utils.Wrap(msg.Content)}{nl}После:{nl}{Utils.Wrap(messageSocket.Content)}";
        await Utils.SilentSendAsync(Utils.GetAdminLogChannel(), toSend);
    }

    private static async Task UserJoinedEvent(SocketGuildUser user) {
        var guild = user.Guild;
        await guild.SystemChannel.SendMessageAsync($"{user.Mention}, добро пожаловать на сервер {guild.Name}");
    }
}