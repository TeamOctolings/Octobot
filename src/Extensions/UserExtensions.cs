using Remora.Discord.API.Abstractions.Objects;

namespace Octobot.Extensions;

public static class UserExtensions
{
    public static string GetTag(this IUser user)
    {
        return user.Discriminator is 0000 ? $"@{user.Username}" : $"{user.Username}#{user.Discriminator:0000}";
    }
}
