using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Rest.Core;

namespace TeamOctolings.Octobot.Extensions;

public static class CommandContextExtensions
{
    public static bool TryGetContextIDs(
        this ICommandContext context, out Snowflake guildId,
        out Snowflake channelId, out Snowflake executorId)
    {
        channelId = default;
        executorId = default;
        return context.TryGetGuildID(out guildId)
               && context.TryGetChannelID(out channelId)
               && context.TryGetUserID(out executorId);
    }
}
