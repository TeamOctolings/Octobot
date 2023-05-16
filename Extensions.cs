using System.Globalization;
using Microsoft.Extensions.Configuration;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;
using Remora.Rest.Core;
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

    public static Result<Snowflake> GetChannel(this Snowflake guildId, string key) {
        var value = Boyfriend.GuildConfiguration.GetValue<ulong?>($"GuildConfigs:{guildId}:{key}");
        return value is not null
            ? Result<Snowflake>.FromSuccess(DiscordSnowflake.New(value.Value))
            : Result<Snowflake>.FromError(new NotFoundError());
    }

    public static CultureInfo GetCulture(this IGuild guild) {
        var value = Boyfriend.GuildConfiguration.GetValue<string?>($"GuildConfigs:{guild.ID}:Language");
        return value is not null ? CultureInfoCache[value] : CultureInfoCache["en"];
    }

    public static EmbedBuilder WithUserFooter(this EmbedBuilder builder, IUser user) {
        var avatarUrlResult = CDN.GetUserAvatarUrl(user, imageSize: 256);
        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity.AbsoluteUri
            : CDN.GetDefaultUserAvatarUrl(user, imageSize: 256).Entity.AbsoluteUri;

        return builder.WithFooter(new EmbedFooter($"{user.Username}#{user.Discriminator:0000}", avatarUrl));
    }

    public static EmbedBuilder WithActionFooter(this EmbedBuilder builder, IUser user) {
        var avatarUrlResult = CDN.GetUserAvatarUrl(user, imageSize: 256);
        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity.AbsoluteUri
            : CDN.GetDefaultUserAvatarUrl(user, imageSize: 256).Entity.AbsoluteUri;

        return builder.WithFooter(
            new EmbedFooter($"{Messages.IssuedBy}:\n{user.Username}#{user.Discriminator:0000}", avatarUrl));
    }

    public static EmbedBuilder WithTitle(this EmbedBuilder builder, IUser avatarSource, string text) {
        var avatarUrlResult = CDN.GetUserAvatarUrl(avatarSource, imageSize: 256);

        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity
            : CDN.GetDefaultUserAvatarUrl(avatarSource, imageSize: 256).Entity;

        builder.Author = new EmbedAuthorBuilder(text, iconUrl: avatarUrl.AbsoluteUri);
        return builder;
    }

    public static string SanitizeForBlockCode(this string s) {
        return s.Replace("```", "​`​`​`​");
    }
}
