using System.Text;
using DiffPlex.DiffBuilder.Model;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Caching;
using Remora.Discord.Caching.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend;

public static class Extensions {
    public static async Task<Result<IUser>> TryGetUserAsync(
        this Snowflake userId, CacheService cacheService, IDiscordRestUserAPI userApi, CancellationToken ct) {
        var cachedUserResult = await cacheService.TryGetValueAsync<IUser>(
            new KeyHelpers.UserCacheKey(userId), ct);

        if (cachedUserResult.IsDefined(out var cachedUser)) return Result<IUser>.FromSuccess(cachedUser);

        return await userApi.GetUserAsync(userId, ct);
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

    public static EmbedBuilder WithEventCover(
        this EmbedBuilder builder, Snowflake eventId, Optional<IImageHash?> imageHashOptional) {
        if (!imageHashOptional.IsDefined(out var imageHash)) return builder;

        var iconUrlResult = CDN.GetGuildScheduledEventCoverUrl(eventId, imageHash, imageSize: 1024);
        return iconUrlResult.IsDefined(out var iconUrl) ? builder.WithImageUrl(iconUrl.AbsoluteUri) : builder;
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

    public static Snowflake ToDiscordSnowflake(this ulong? id) {
        return DiscordSnowflake.New(id ?? 0);
    }
}
