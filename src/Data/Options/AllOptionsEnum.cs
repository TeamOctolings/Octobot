using Boyfriend.Commands;
using JetBrains.Annotations;

namespace Boyfriend.Data.Options;

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
    [UsedImplicitly] WelcomeMessage,
    [UsedImplicitly] ReceiveStartupMessages,
    [UsedImplicitly] RemoveRolesOnMute,
    [UsedImplicitly] ReturnRolesOnRejoin,
    [UsedImplicitly] AutoStartEvents,
    [UsedImplicitly] RenameHoistedUsers,
    [UsedImplicitly] PublicFeedbackChannel,
    [UsedImplicitly] PrivateFeedbackChannel,
    [UsedImplicitly] EventNotificationChannel,
    [UsedImplicitly] DefaultRole,
    [UsedImplicitly] MuteRole,
    [UsedImplicitly] EventNotificationRole,
    [UsedImplicitly] EventEarlyNotificationOffset
}
