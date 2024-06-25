using System.Text;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Extensions.Formatting;
using Remora.Rest.Core;
using Remora.Results;
using TeamOctolings.Octobot.Data;
using TeamOctolings.Octobot.Extensions;

namespace TeamOctolings.Octobot.Services;

public sealed class ReminderService
{
    private readonly IDiscordRestChannelAPI _channelApi;

    public ReminderService(IDiscordRestChannelAPI channelApi)
    {
        _channelApi = channelApi;
    }

    public async Task TickRemindersAsync(Snowflake guildId, IUser user, List<Reminder> reminders, List<Result> failedResults, CancellationToken ct = default)
    {
        for (var i = reminders.Count - 1; i >= 0; i--)
        {
            var reminder = reminders[i];
            if (DateTimeOffset.UtcNow < reminder.At)
            {
                continue;
            }

            var sendResult = await SendReminderAsync(guildId, user, reminders[i], ct);
            failedResults.AddIfFailed(sendResult);
            if (sendResult.IsSuccess)
            {
                reminders.RemoveAt(i);
            }
        }
    }

    private async Task<Result> SendReminderAsync(Snowflake guildId, IUser user, Reminder reminder, CancellationToken ct = default)
    {
        var builder = new StringBuilder()
            .AppendBulletPointLine(string.Format(Messages.DescriptionReminder, Markdown.InlineCode(reminder.Text)))
            .AppendBulletPointLine(string.Format(Messages.DescriptionActionJumpToMessage,
                $"https://discord.com/channels/{guildId.Value}/{reminder.ChannelId}/{reminder.MessageId}"));

        var embed = new EmbedBuilder().WithSmallTitle(
                string.Format(Messages.Reminder, user.GetTag()), user)
            .WithDescription(builder.ToString())
            .WithColour(ColorsList.Magenta)
            .Build();

        return await _channelApi.CreateMessageWithEmbedResultAsync(
            reminder.ChannelId.ToSnowflake(), Mention.User(user), embedResult: embed, ct: ct);
    }
}
