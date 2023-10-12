using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.Extensions.Embeds;
using Remora.Rest.Core;

namespace Octobot.Extensions;

public static class EmbedBuilderExtensions
{
    /// <summary>
    ///     Adds a footer representing that an action was performed by a <paramref name="user" />.
    /// </summary>
    /// <param name="builder">The builder to add the footer to.</param>
    /// <param name="user">The user that performed the action whose tag and avatar to use.</param>
    /// <returns>The builder with the added footer.</returns>
    public static EmbedBuilder WithActionFooter(this EmbedBuilder builder, IUser user)
    {
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
        this EmbedBuilder builder, string text, IUser? avatarSource = null)
    {
        Uri? avatarUrl = null;
        if (avatarSource is not null)
        {
            var avatarUrlResult = CDN.GetUserAvatarUrl(avatarSource, imageSize: 256);

            avatarUrl = avatarUrlResult.IsSuccess
                ? avatarUrlResult.Entity
                : CDN.GetDefaultUserAvatarUrl(avatarSource, imageSize: 256).Entity;
        }

        builder.Author = new EmbedAuthorBuilder(text, iconUrl: avatarUrl?.AbsoluteUri);
        return builder;
    }

    /// <summary>
    ///     Adds a user avatar in the thumbnail field.
    /// </summary>
    /// <param name="builder">The builder to add the thumbnail to.</param>
    /// <param name="avatarSource">The user whose avatar to use in the thumbnail field.</param>
    /// <returns>The builder with the added avatar in the thumbnail field.</returns>
    public static EmbedBuilder WithLargeUserAvatar(
        this EmbedBuilder builder, IUser avatarSource)
    {
        var avatarUrlResult = CDN.GetUserAvatarUrl(avatarSource, imageSize: 256);
        var avatarUrl = avatarUrlResult.IsSuccess
            ? avatarUrlResult.Entity
            : CDN.GetDefaultUserAvatarUrl(avatarSource, imageSize: 256).Entity;

        return builder.WithThumbnailUrl(avatarUrl.AbsoluteUri);
    }

    /// <summary>
    ///     Adds a guild icon in the thumbnail field.
    /// </summary>
    /// <param name="builder">The builder to add the thumbnail to.</param>
    /// <param name="iconSource">The guild whose icon to use in the thumbnail field.</param>
    /// <returns>The builder with the added icon in the thumbnail field.</returns>
    public static EmbedBuilder WithLargeGuildIcon(
        this EmbedBuilder builder, IGuild iconSource)
    {
        var iconUrlResult = CDN.GetGuildIconUrl(iconSource, imageSize: 256);
        return iconUrlResult.IsSuccess
            ? builder.WithThumbnailUrl(iconUrlResult.Entity.AbsoluteUri)
            : builder;
    }

    /// <summary>
    ///     Adds a guild banner in the image field.
    /// </summary>
    /// <param name="builder">The builder to add the image to.</param>
    /// <param name="bannerSource">The guild whose banner to use in the image field.</param>
    /// <returns>The builder with the added banner in the image field.</returns>
    public static EmbedBuilder WithGuildBanner(
        this EmbedBuilder builder, IGuild bannerSource)
    {
        return bannerSource.Banner is not null
            ? builder.WithImageUrl(CDN.GetGuildBannerUrl(bannerSource).Entity.AbsoluteUri)
            : builder;
    }

    /// <summary>
    ///     Adds a footer representing that the action was performed in the <paramref name="guild" />.
    /// </summary>
    /// <param name="builder">The builder to add the footer to.</param>
    /// <param name="guild">The guild whose name and icon to use.</param>
    /// <returns>The builder with the added footer.</returns>
    public static EmbedBuilder WithGuildFooter(this EmbedBuilder builder, IGuild guild)
    {
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
    public static EmbedBuilder WithGuildTitle(this EmbedBuilder builder, IGuild guild)
    {
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
        this EmbedBuilder builder, Snowflake eventId, Optional<IImageHash?> imageHashOptional)
    {
        if (!imageHashOptional.IsDefined(out var imageHash))
        {
            return builder;
        }

        var iconUrlResult = CDN.GetGuildScheduledEventCoverUrl(eventId, imageHash, imageSize: 1024);
        return iconUrlResult.IsDefined(out var iconUrl) ? builder.WithImageUrl(iconUrl.AbsoluteUri) : builder;
    }
}
