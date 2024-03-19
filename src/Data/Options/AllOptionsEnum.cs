using JetBrains.Annotations;
using Octobot.Commands;

namespace Octobot.Data.Options;

/// <summary>
///     Represents all options as enums.
/// </summary>
/// <remarks>
///     WARNING: This enum is order-dependent! It's values are used as indexes for
///     <see cref="SettingsCommandGroup.AllOptions" />.
/// </remarks>
public enum AllOptionsEnum
{
    [UsedImplicitly] Language,
    [UsedImplicitly] WarnPunishment,
    [UsedImplicitly] WelcomeMessage,
    [UsedImplicitly] ReceiveStartupMessages,
    [UsedImplicitly] RemoveRolesOnMute,
    [UsedImplicitly] ReturnRolesOnRejoin,
    [UsedImplicitly] AutoStartEvents,
    [UsedImplicitly] RenameHoistedUsers,
    [UsedImplicitly] WarnsThreshold,
    [UsedImplicitly] PublicFeedbackChannel,
    [UsedImplicitly] PrivateFeedbackChannel,
    [UsedImplicitly] WelcomeMessagesChannel,
    [UsedImplicitly] EventNotificationChannel,
    [UsedImplicitly] DefaultRole,
    [UsedImplicitly] MuteRole,
    [UsedImplicitly] EventNotificationRole,
    [UsedImplicitly] EventEarlyNotificationOffset,
    [UsedImplicitly] WarnPunishmentDuration
}
