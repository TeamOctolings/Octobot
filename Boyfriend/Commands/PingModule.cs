using Discord.Commands;

namespace Boyfriend.Commands;

public class PingModule : ModuleBase<SocketCommandContext> {

    [Command("ping")]
    [Summary("Измеряет время обработки REST-запроса")]
    [Alias("пинг")]
    public async Task Run()
        => await ReplyAsync(Utils.GetBeep() + Boyfriend.Client.Latency + "мс");
}