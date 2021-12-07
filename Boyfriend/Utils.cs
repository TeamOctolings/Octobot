using Discord;

namespace Boyfriend;

public static class Utils {
    public static string GetBeep() {
        var letters = new[] { "а", "о", "и"};
        return "Б" + letters[new Random().Next(3)] + "п! ";
    }

    [Obsolete("Stop hard-coding things!")]
    public static IMessageChannel GetAdminLogChannel() {
        if (Boyfriend.Client.GetChannel(870929165141032971) is not IMessageChannel adminLogChannel)
            throw new ArgumentException("Invalid admin log channel");
        return adminLogChannel;
    }

    public static string Wrap(string original) {
        return original.Trim().Equals("") ? "" : "```" + original.Replace("```", "​`​`​`​") + "```";
    }

    public static string MentionChannel(ulong id) {
        return "<#" + id + ">";
    }
}