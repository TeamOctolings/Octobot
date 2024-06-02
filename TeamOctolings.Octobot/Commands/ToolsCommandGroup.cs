using System.ComponentModel;
using System.Text;
using JetBrains.Annotations;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Results;
using TeamOctolings.Octobot.Data;
using TeamOctolings.Octobot.Extensions;
using TeamOctolings.Octobot.Parsers;
using TeamOctolings.Octobot.Services;

namespace TeamOctolings.Octobot.Commands;

/// <summary>
///     Handles tool commands: /random, /timestamp, /8ball.
/// </summary>
[UsedImplicitly]
public sealed class ToolsCommandGroup : CommandGroup
{
    private static readonly TimestampStyle[] AllStyles =
    [
        TimestampStyle.ShortDate,
        TimestampStyle.LongDate,
        TimestampStyle.ShortTime,
        TimestampStyle.LongTime,
        TimestampStyle.ShortDateTime,
        TimestampStyle.LongDateTime,
        TimestampStyle.RelativeTime
    ];

    private static readonly string[] AnswerTypes =
    [
        "Positive", "Questionable", "Neutral", "Negative"
    ];

    private readonly ICommandContext _context;
    private readonly IFeedbackService _feedback;
    private readonly GuildDataService _guildData;
    private readonly IDiscordRestUserAPI _userApi;

    public ToolsCommandGroup(
        ICommandContext context, IFeedbackService feedback,
        GuildDataService guildData, IDiscordRestUserAPI userApi)
    {
        _context = context;
        _guildData = guildData;
        _feedback = feedback;
        _userApi = userApi;
    }

    /// <summary>
    ///     A slash command that generates a random number using maximum and minimum numbers.
    /// </summary>
    /// <param name="first">The first number used for randomization.</param>
    /// <param name="second">The second number used for randomization. Default value: 0</param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("random")]
    [DiscordDefaultDMPermission(false)]
    [Description("Generates a random number")]
    [UsedImplicitly]
    public async Task<Result> ExecuteRandomAsync(
        [Description("First number")] long first,
        [Description("Second number (Default: 0)")]
        long? second = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return ResultExtensions.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await SendRandomNumberAsync(first, second, executor, CancellationToken);
    }

    private Task<Result> SendRandomNumberAsync(long first, long? secondNullable,
        IUser executor, CancellationToken ct)
    {
        const long secondDefault = 0;
        var second = secondNullable ?? secondDefault;

        var min = Math.Min(first, second);
        var max = Math.Max(first, second);

        var i = Random.Shared.NextInt64(min, max + 1);

        var description = new StringBuilder().Append("# ").Append(i);

        description.AppendLine().AppendBulletPoint(string.Format(
            Messages.RandomMin, Markdown.InlineCode(min.ToString())));
        if (secondNullable is null && first >= secondDefault)
        {
            description.Append(' ').Append(Messages.Default);
        }

        description.AppendLine().AppendBulletPoint(string.Format(
            Messages.RandomMax, Markdown.InlineCode(max.ToString())));
        if (secondNullable is null && first < secondDefault)
        {
            description.Append(' ').Append(Messages.Default);
        }

        var embedColor = ColorsList.Blue;
        if (secondNullable is not null && min == max)
        {
            description.AppendLine().Append(Markdown.Italicise(Messages.RandomMinMaxSame));
            embedColor = ColorsList.Red;
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.RandomTitle, executor.GetTag()), executor)
            .WithDescription(description.ToString())
            .WithColour(embedColor)
            .Build();

        return _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    /// <summary>
    ///     A slash command that shows the current timestamp with an optional offset in all styles supported by Discord.
    /// </summary>
    /// <param name="stringOffset">The offset for the current timestamp.</param>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("timestamp")]
    [DiscordDefaultDMPermission(false)]
    [Description("Shows a timestamp in all styles")]
    [UsedImplicitly]
    public async Task<Result> ExecuteTimestampAsync(
        [Description("Offset from current time")] [Option("offset")]
        string? stringOffset = null)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out var executorId))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return ResultExtensions.FromError(botResult);
        }

        var executorResult = await _userApi.GetUserAsync(executorId, CancellationToken);
        if (!executorResult.IsDefined(out var executor))
        {
            return ResultExtensions.FromError(executorResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        if (stringOffset is null)
        {
            return await SendTimestampAsync(null, executor, CancellationToken);
        }

        var parseResult = TimeSpanParser.TryParse(stringOffset);
        if (!parseResult.IsDefined(out var offset))
        {
            var failedEmbed = new EmbedBuilder()
                .WithSmallTitle(Messages.InvalidTimeSpan, bot)
                .WithDescription(Messages.TimeSpanExample)
                .WithColour(ColorsList.Red)
                .Build();

            return await _feedback.SendContextualEmbedResultAsync(failedEmbed, ct: CancellationToken);
        }

        return await SendTimestampAsync(offset, executor, CancellationToken);
    }

    private Task<Result> SendTimestampAsync(TimeSpan? offset, IUser executor, CancellationToken ct)
    {
        var timestamp = DateTimeOffset.UtcNow.Add(offset ?? TimeSpan.Zero).ToUnixTimeSeconds();

        var description = new StringBuilder().Append("# ").AppendLine(timestamp.ToString());

        if (offset is not null)
        {
            description.AppendLine(string.Format(
                Messages.TimestampOffset, Markdown.InlineCode(offset.ToString() ?? string.Empty))).AppendLine();
        }

        foreach (var markdownTimestamp in AllStyles.Select(style => Markdown.Timestamp(timestamp, style)))
        {
            description.AppendBulletPoint(Markdown.InlineCode(markdownTimestamp))
                .Append(" → ").AppendLine(markdownTimestamp);
        }

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.TimestampTitle, executor.GetTag()), executor)
            .WithDescription(description.ToString())
            .WithColour(ColorsList.Blue)
            .Build();

        return _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }

    /// <summary>
    ///     A slash command that shows a random answer from the Magic 8-Ball.
    /// </summary>
    /// <param name="question">Unused input.</param>
    /// <remarks>
    ///     The 8-Ball answers were taken from <a href="https://en.wikipedia.org/wiki/Magic_8_Ball#Possible_answers">Wikipedia</a>.
    /// </remarks>
    /// <returns>
    ///     A feedback sending result which may or may not have succeeded.
    /// </returns>
    [Command("8ball")]
    [DiscordDefaultDMPermission(false)]
    [Description("Ask the Magic 8-Ball a question")]
    [UsedImplicitly]
    public async Task<Result> ExecuteEightBallAsync(
        // let the user think he's actually asking the ball a question
        [Description("Question to ask")] string question)
    {
        if (!_context.TryGetContextIDs(out var guildId, out _, out _))
        {
            return new ArgumentInvalidError(nameof(_context), "Unable to retrieve necessary IDs from command context");
        }

        var botResult = await _userApi.GetCurrentUserAsync(CancellationToken);
        if (!botResult.IsDefined(out var bot))
        {
            return ResultExtensions.FromError(botResult);
        }

        var data = await _guildData.GetData(guildId, CancellationToken);
        Messages.Culture = GuildSettings.Language.Get(data.Settings);

        return await AnswerEightBallAsync(bot, CancellationToken);
    }

    private Task<Result> AnswerEightBallAsync(IUser bot, CancellationToken ct)
    {
        var typeNumber = Random.Shared.Next(0, 4);
        var embedColor = typeNumber switch
        {
            0 => ColorsList.Blue,
            1 => ColorsList.Green,
            2 => ColorsList.Yellow,
            3 => ColorsList.Red,
            _ => throw new ArgumentOutOfRangeException(null, nameof(typeNumber))
        };

        var answer = $"EightBall{AnswerTypes[typeNumber]}{Random.Shared.Next(1, 6)}".Localized();

        var embed = new EmbedBuilder().WithSmallTitle(answer, bot)
            .WithColour(embedColor)
            .Build();

        return _feedback.SendContextualEmbedResultAsync(embed, ct: ct);
    }
}
