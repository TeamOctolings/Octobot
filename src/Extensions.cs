using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using DiffPlex.DiffBuilder.Model;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;

namespace Boyfriend;

public static class Extensions {
    /// <summary>
    ///     Adds a footer with the <paramref name="user" />'s avatar and tag (@username or username#0000).
    /// </summary>
    /// <param name="builder">The builder to add the footer to.</param>
    /// <param name="user">The user whose tag and avatar to add.</param>
    /// <returns>The builder with the added footer.</returns>
    public static EmbedBuilder WithUserFooter(this EmbedBuilder builder, IUser user) {
        var avatarUrlResult = CDN.GetUserAvatarUrl(user, imageSize: 256);
        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity.AbsoluteUri
            : CDN.GetDefaultUserAvatarUrl(user, imageSize: 256).Entity.AbsoluteUri;

        return builder.WithFooter(new EmbedFooter(user.GetTag(), avatarUrl));
    }

    /// <summary>
    ///     Adds a footer representing that an action was performed by a <paramref name="user" />.
    /// </summary>
    /// <param name="builder">The builder to add the footer to.</param>
    /// <param name="user">The user that performed the action whose tag and avatar to use.</param>
    /// <returns>The builder with the added footer.</returns>
    public static EmbedBuilder WithActionFooter(this EmbedBuilder builder, IUser user) {
        var avatarUrlResult = CDN.GetUserAvatarUrl(user, imageSize: 256);
        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity.AbsoluteUri
            : CDN.GetDefaultUserAvatarUrl(user, imageSize: 256).Entity.AbsoluteUri;

        return builder.WithFooter(
            new EmbedFooter($"{Messages.IssuedBy}:\n{user.GetTag()}", avatarUrl));
    }

    /// <summary>
    ///     Adds a title using the author field, making it smaller than using the title field.
    /// </summary>
    /// <param name="builder">The builder to add the small title to.</param>
    /// <param name="text">The text of the small title.</param>
    /// <param name="avatarSource">The user whose avatar to use in the small title.</param>
    /// <returns>The builder with the added small title in the author field.</returns>
    public static EmbedBuilder WithSmallTitle(
        this EmbedBuilder builder, string text, IUser? avatarSource = null) {
        Uri? avatarUrl = null;
        if (avatarSource is not null) {
            var avatarUrlResult = CDN.GetUserAvatarUrl(avatarSource, imageSize: 256);

            avatarUrl = avatarUrlResult.IsSuccess
                ? avatarUrlResult.Entity
                : CDN.GetDefaultUserAvatarUrl(avatarSource, imageSize: 256).Entity;
        }

        builder.Author = new EmbedAuthorBuilder(text, iconUrl: avatarUrl?.AbsoluteUri);
        return builder;
    }

    /// <summary>
    ///     Adds a footer representing that the action was performed in the <paramref name="guild" />.
    /// </summary>
    /// <param name="builder">The builder to add the footer to.</param>
    /// <param name="guild">The guild whose name and icon to use.</param>
    /// <returns>The builder with the added footer.</returns>
    public static EmbedBuilder WithGuildFooter(this EmbedBuilder builder, IGuild guild) {
        var iconUrlResult = CDN.GetGuildIconUrl(guild, imageSize: 256);
        var iconUrl = iconUrlResult.IsSuccess
            ? iconUrlResult.Entity.AbsoluteUri
            : default(Optional<string>);

        return builder.WithFooter(new EmbedFooter(guild.Name, iconUrl));
    }

    /// <summary>
    ///     Adds a title representing that the action happened in the <paramref name="guild" />.
    /// </summary>
    /// <param name="builder">The builder to add the title to.</param>
    /// <param name="guild">The guild whose name and icon to use.</param>
    /// <returns>The builder with the added title.</returns>
    public static EmbedBuilder WithGuildTitle(this EmbedBuilder builder, IGuild guild) {
        var iconUrlResult = CDN.GetGuildIconUrl(guild, imageSize: 256);
        var iconUrl = iconUrlResult.IsSuccess
            ? iconUrlResult.Entity.AbsoluteUri
            : null;

        builder.Author = new EmbedAuthorBuilder(guild.Name, iconUrl: iconUrl);
        return builder;
    }

    /// <summary>
    ///     Adds a scheduled event's cover image.
    /// </summary>
    /// <param name="builder">The builder to add the image to.</param>
    /// <param name="eventId">The ID of the scheduled event whose image to use.</param>
    /// <param name="imageHashOptional">The Optional containing the image hash.</param>
    /// <returns>The builder with the added cover image.</returns>
    public static EmbedBuilder WithEventCover(
        this EmbedBuilder builder, Snowflake eventId, Optional<IImageHash?> imageHashOptional) {
        if (!imageHashOptional.IsDefined(out var imageHash)) return builder;

        var iconUrlResult = CDN.GetGuildScheduledEventCoverUrl(eventId, imageHash, imageSize: 1024);
        return iconUrlResult.IsDefined(out var iconUrl) ? builder.WithImageUrl(iconUrl.AbsoluteUri) : builder;
    }

    /// <summary>
    ///     Sanitizes a string for use in <see cref="Markdown.BlockCode(string)" /> by inserting zero-width spaces in between
    ///     symbols used to format the string with block code.
    /// </summary>
    /// <param name="s">The string to sanitize.</param>
    /// <returns>The sanitized string that can be safely used in <see cref="Markdown.BlockCode(string)" />.</returns>
    private static string SanitizeForBlockCode(this string s) {
        return s.Replace("```", "​`​`​`​");
    }

    /// <summary>
    /// Sanitizes a string (see <see cref="SanitizeForBlockCode" />) and formats the string to use Markdown Block Code formatting with a specified
    /// language for syntax highlighting.
    /// </summary>
    /// <param name="s">The string to sanitize and format.</param>
    /// <param name="language"></param>
    /// <returns>The sanitized string formatted to use Markdown Block Code with a specified
    /// language for syntax highlighting.</returns>
    public static string InBlockCode(this string s, string language = "") {
        s = s.SanitizeForBlockCode();
        return
            $"```{language}\n{s.SanitizeForBlockCode()}{(s.EndsWith("`") || string.IsNullOrWhiteSpace(s) ? " " : "")}```";
    }

    public static string Localized(this string key) {
        return Messages.ResourceManager.GetString(key, Messages.Culture) ?? key;
    }

    /// <summary>
    ///     Encodes a string to allow its transmission in request headers.
    /// </summary>
    /// <remarks>Used when encountering "Request headers must contain only ASCII characters".</remarks>
    /// <param name="s">The string to encode.</param>
    /// <returns>An encoded string with spaces kept intact.</returns>
    public static string EncodeHeader(this string s) {
        return WebUtility.UrlEncode(s).Replace('+', ' ');
    }

    public static string AsMarkdown(this DiffPaneModel model) {
        var builder = new StringBuilder();
        foreach (var line in model.Lines) {
            if (line.Type is ChangeType.Deleted)
                builder.Append("-- ");
            if (line.Type is ChangeType.Inserted)
                builder.Append("++ ");
            if (line.Type is not ChangeType.Imaginary)
                builder.AppendLine(line.Text);
        }

        return InBlockCode(builder.ToString(), "diff");
    }

    public static string GetTag(this IUser user) {
        return user.Discriminator is 0000 ? $"@{user.Username}" : $"{user.Username}#{user.Discriminator:0000}";
    }

    public static Snowflake ToSnowflake(this ulong id) {
        return DiscordSnowflake.New(id);
    }

    public static TResult? MaxOrDefault<TSource, TResult>(
        this IEnumerable<TSource> source, Func<TSource, TResult> selector) {
        var list = source.ToList();
        return list.Any() ? list.Max(selector) : default;
    }

    public static bool TryGetContextIDs(
        this                    ICommandContext context,   [NotNullWhen(true)] out Snowflake? guildId,
        [NotNullWhen(true)] out Snowflake?      channelId, [NotNullWhen(true)] out Snowflake? userId) {
        guildId = null;
        channelId = null;
        userId = null;
        return context.TryGetGuildID(out guildId)
               && context.TryGetChannelID(out channelId)
               && context.TryGetUserID(out userId);
    }

    /// <summary>
    ///     Checks whether this Snowflake has any value set.
    /// </summary>
    /// <param name="snowflake">The Snowflake to check.</param>
    /// <returns>true if the Snowflake has no value set or it's set to 0, false otherwise.</returns>
    public static bool Empty(this Snowflake snowflake) {
        return snowflake.Value is 0;
    }

    /// <summary>
    ///     Checks whether this snowflake is empty (see <see cref="Empty" />) or it's equal to
    ///     <paramref name="anotherSnowflake" />
    /// </summary>
    /// <param name="snowflake">The Snowflake to check for emptiness</param>
    /// <param name="anotherSnowflake">The Snowflake to check for equality with <paramref name="snowflake" />.</param>
    /// <returns>
    ///     true if <paramref name="snowflake" /> is empty or is equal to <paramref name="anotherSnowflake" />, false
    ///     otherwise.
    /// </returns>
    /// <seealso cref="Empty" />
    public static bool EmptyOrEqualTo(this Snowflake snowflake, Snowflake anotherSnowflake) {
        return snowflake.Empty() || snowflake == anotherSnowflake;
    }

    public static async Task<Result> SendContextualEmbedResultAsync(
        this FeedbackService feedback, Result<Embed> embedResult, CancellationToken ct = default) {
        if (!embedResult.IsDefined(out var embed))
            return Result.FromError(embedResult);

        return (Result)await feedback.SendContextualEmbedAsync(embed, ct: ct);
    }
}
