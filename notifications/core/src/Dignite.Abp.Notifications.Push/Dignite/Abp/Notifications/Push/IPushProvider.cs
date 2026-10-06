using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// A push delivery service (Expo, FCM, APNs, ...) behind the one <c>"Push"</c> channel. A notification definition only
/// ever names the channel; which provider carries a message is decided per device, by <see cref="PushTarget.Provider"/>.
/// </summary>
public interface IPushProvider
{
    /// <summary>
    /// Stable provider name stored on each device registration and matched case-insensitively, e.g. <c>"Expo"</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sends the messages and reports one result per message. Delivery is best-effort: no retry. A provider that
    /// cannot reach its service at all may throw; a per-message rejection is a <see cref="PushSendStatus.Failed"/>
    /// result, and a dead device a <see cref="PushSendStatus.TokenInvalid"/> one.
    /// </summary>
    Task<IReadOnlyList<PushSendResult>> SendAsync(
        IReadOnlyList<PushMessage> messages,
        CancellationToken cancellationToken = default);
}
