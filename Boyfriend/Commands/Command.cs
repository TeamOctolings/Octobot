using System.Text;
using Discord.Commands;

namespace Boyfriend.Commands;

public abstract class Command {
    public abstract string[] Aliases { get; }

    public abstract int ArgsLengthRequired { get; }
    public abstract Task Run(SocketCommandContext context, string[] args);

    protected static void Output(ref StringBuilder message) {
        CommandHandler.StackedReplyMessage.Append(message).AppendLine();
    }

    protected static void Success(string message, string userMention, bool sendPublicFeedback = false,
        bool sendPrivateFeedback = true) {
        CommandHandler.StackedReplyMessage.Append(":white_check_mark: ").AppendLine(message);
        if (sendPrivateFeedback)
            Utils.StackFeedback(ref message, ref userMention, sendPublicFeedback);
    }

    protected static void Warn(string message) {
        CommandHandler.StackedReplyMessage.Append(":warning: ").AppendLine(message);
    }

    protected static void Error(string message, bool accessDenied) {
        var symbol = accessDenied ? ":no_entry_sign: " : ":x: ";
        CommandHandler.StackedReplyMessage.Append(symbol).AppendLine(message);
    }
}