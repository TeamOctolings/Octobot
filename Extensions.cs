using System.Globalization;
using Microsoft.Extensions.Configuration;
using Remora.Discord.API.Abstractions.Gateway.Events;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Results;

namespace Boyfriend;

public static class Extensions {
    private static readonly Dictionary<string, CultureInfo> CultureInfoCache = new() {
        { "en", new CultureInfo("en-US") },
        { "ru", new CultureInfo("ru-RU") },
        { "mctaylors-ru", new CultureInfo("tt-RU") }
    };

    public static Result<bool> GetConfigBool(this IGuild guild, string key) {
        var value = Boyfriend.GuildConfiguration.GetValue<bool?>($"GuildConfigs:{guild.ID}:{key}");
        return value is not null ? Result<bool>.FromSuccess(value.Value) : Result<bool>.FromError(new NotFoundError());
    }

    public static Result<IChannel> GetChannel(this IGuildCreate.IAvailableGuild guild, string key) {
        var value = Boyfriend.GuildConfiguration.GetValue<ulong?>($"GuildConfigs:{guild.ID}:{key}");
        if (value is null) return Result<IChannel>.FromError(new NotFoundError());

        var match = guild.Channels.SingleOrDefault(channel => channel!.ID.Equals(value.Value), null);
        return match is not null
            ? Result<IChannel>.FromSuccess(match)
            : Result<IChannel>.FromError(new NotFoundError());
    }

    public static CultureInfo GetCulture(this IGuild guild) {
        var value = Boyfriend.GuildConfiguration.GetValue<string?>($"GuildConfigs:{guild.ID}:Language");
        return value is not null ? CultureInfoCache[value] : CultureInfoCache["en"];
    }
}
