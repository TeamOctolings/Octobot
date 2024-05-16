using Remora.Rest.Core;

namespace TeamOctolings.Octobot.Extensions;

public static class SnowflakeExtensions
{
    /// <summary>
    ///     Checks whether this Snowflake has any value set.
    /// </summary>
    /// <param name="snowflake">The Snowflake to check.</param>
    /// <returns>true if the Snowflake has no value set or it's set to 0, false otherwise.</returns>
    public static bool Empty(this Snowflake snowflake)
    {
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
    public static bool EmptyOrEqualTo(this Snowflake snowflake, Snowflake anotherSnowflake)
    {
        return snowflake.Empty() || snowflake == anotherSnowflake;
    }
}
