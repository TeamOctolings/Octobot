using System.Globalization;
using System.Text;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Extensions.Configuration;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
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

    public static Result<Snowflake> GetConfigChannel(this Snowflake guildId, string key) {
        var value = Boyfriend.GuildConfiguration.GetValue<ulong?>($"GuildConfigs:{guildId}:{key}");
        return value is not null
            ? Result<Snowflake>.FromSuccess(DiscordSnowflake.New(value.Value))
            : Result<Snowflake>.FromError(new NotFoundError());
    }

    public static Result<string> GetConfigString(this Snowflake guildId, string key) {
        var value = Boyfriend.GuildConfiguration.GetValue<string?>($"GuildConfigs:{guildId}:{key}");
        return value is not null ? Result<string>.FromSuccess(value) : Result<string>.FromError(new NotFoundError());
    }

    public static CultureInfo GetGuildCulture(this Snowflake guildId) {
        var value = Boyfriend.GuildConfiguration.GetValue<string?>($"GuildConfigs:{guildId}:Language");
        return value is not null ? CultureInfoCache[value] : CultureInfoCache["en"];
    }

    public static EmbedBuilder WithUserFooter(this EmbedBuilder builder, IUser user) {
        var avatarUrlResult = CDN.GetUserAvatarUrl(user, imageSize: 256);
        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity.AbsoluteUri
            : CDN.GetDefaultUserAvatarUrl(user, imageSize: 256).Entity.AbsoluteUri;

        return builder.WithFooter(new EmbedFooter(user.GetTag(), avatarUrl));
    }

    public static EmbedBuilder WithActionFooter(this EmbedBuilder builder, IUser user) {
        var avatarUrlResult = CDN.GetUserAvatarUrl(user, imageSize: 256);
        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity.AbsoluteUri
            : CDN.GetDefaultUserAvatarUrl(user, imageSize: 256).Entity.AbsoluteUri;

        return builder.WithFooter(
            new EmbedFooter($"{Messages.IssuedBy}:\n{user.GetTag()}", avatarUrl));
    }

    public static EmbedBuilder WithSmallTitle(
        this EmbedBuilder builder, IUser avatarSource, string text, string? url = default) {
        var avatarUrlResult = CDN.GetUserAvatarUrl(avatarSource, imageSize: 256);

        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity
            : CDN.GetDefaultUserAvatarUrl(avatarSource, imageSize: 256).Entity;

        builder.Author = new EmbedAuthorBuilder(text, url, avatarUrl.AbsoluteUri);
        return builder;
    }

    public static EmbedBuilder WithGuildFooter(this EmbedBuilder builder, IGuild guild) {
        var iconUrlResult = CDN.GetGuildIconUrl(guild, imageSize: 256);
        var iconUrl = iconUrlResult.IsSuccess
            ? iconUrlResult.Entity.AbsoluteUri
            : default(Optional<string>);

        return builder.WithFooter(new EmbedFooter(guild.Name, iconUrl));
    }

    public static string SanitizeForBlockCode(this string s) {
        return s.Replace("```", "​`​`​`​");
    }

    public static string AsMarkdown(this SideBySideDiffModel model) {
        var builder = new StringBuilder();
        foreach (var line in model.OldText.Lines.Where(piece => !string.IsNullOrWhiteSpace(piece.Text)))
            builder.Append("-- ").AppendLine(line.Text);
        foreach (var line in model.NewText.Lines) builder.Append("++ ").AppendLine(line.Text);

        return Markdown.BlockCode(builder.ToString().SanitizeForBlockCode(), "diff");
    }

    public static string GetTag(this IUser user) {
        return $"{user.Username}#{user.Discriminator:0000}";
    }
}
