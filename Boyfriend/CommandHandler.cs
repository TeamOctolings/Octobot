using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Boyfriend;

public static class CommandHandler {
    public static async Task HandleCommand(SocketUserMessage message, int argPos) {
        var context = new SocketCommandContext(Boyfriend.Client, message);
        var result = await EventHandler.Commands.ExecuteAsync(context, argPos, null);

        await HandleErrors(context, result);
    }
    private static async Task HandleErrors(SocketCommandContext context, IResult result) {
        var channel = context.Channel;
        var reason = Utils.WrapInline(result.ErrorReason);
        switch (result.Error) {
            case CommandError.Exception:
                await channel.SendMessageAsync(reason);
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
                await channel.SendMessageAsync($"У тебя недостаточно прав для выполнения этой к: {reason}");
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
                throw new Exception("CommandError");
        }
    }

    public static async Task CheckPermissions(IGuildUser user, GuildPermission toCheck,
        GuildPermission forBot = GuildPermission.StartEmbeddedActivities) {
        if (forBot == GuildPermission.StartEmbeddedActivities) forBot = toCheck;
        if (!(await user.Guild.GetCurrentUserAsync()).GuildPermissions.Has(forBot))
            throw new Exception("У меня недостаточно прав для выполнения этой команды!");
        if (!user.GuildPermissions.Has(toCheck))
            throw new Exception("У тебя недостаточно прав для выполнения этой команды!");
    }

    public static async Task CheckInteractions(IGuildUser actor, IGuildUser target) {
        if (actor.Guild != target.Guild)
            throw new Exception("Участники находятся в разных гильдиях!");
        var me = await target.Guild.GetCurrentUserAsync();
        if (actor.Id == actor.Guild.OwnerId) return;
        if (target.Id == target.Guild.OwnerId)
            throw new Exception("Ты не можешь взаимодействовать с владельцем сервера!");
        if (actor == target)
            throw new Exception("Ты не можешь взаимодействовать с самим собой!");
        if (target == me)
            throw new Exception("Ты не можешь со мной взаимодействовать!");
        if (actor.Hierarchy <= target.Hierarchy)
            throw new Exception("Ты не можешь взаимодействовать с этим участником!");
        if (me.Hierarchy <= target.Hierarchy)
            throw new Exception("Я не могу взаимодействовать с этим участником!");
    }
}