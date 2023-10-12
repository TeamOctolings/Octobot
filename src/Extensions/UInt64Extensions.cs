using Remora.Discord.API;
using Remora.Rest.Core;

namespace Octobot.Extensions;

public static class UInt64Extensions
{
    public static Snowflake ToSnowflake(this ulong id)
    {
        return DiscordSnowflake.New(id);
    }
}
