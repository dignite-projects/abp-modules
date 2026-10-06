using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Push;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace Dignite.NotificationCenter.Push.Identity;

/// <summary>
/// Makes push follow the ABP login session: a device registered under a session that no longer exists — signed out,
/// revoked by an administrator, ended by the concurrent-login rule, or cleaned up as inactive — is forgotten instead
/// of pushed to. This covers the sign-outs the app could not report, such as a forced sign-out after its refresh
/// token expired.
/// </summary>
/// <remarks>
/// Installing the package is the switch. It is only meaningful on a host that actually maintains
/// <see cref="IdentitySession"/> rows and issues the <c>session_id</c> claim — ABP Identity Pro's session management.
/// On a host that does not, devices register without a session id and are all treated as active, so the package is
/// inert rather than harmful.
/// </remarks>
[Dependency(ReplaceServices = true)]
[ExposeServices(
    typeof(IPushDeviceStore),
    typeof(NotificationCenterPushDeviceStore),
    typeof(IdentitySessionPushDeviceStore))]
public class IdentitySessionPushDeviceStore : NotificationCenterPushDeviceStore
{
    protected IIdentitySessionRepository SessionRepository { get; }

    public IdentitySessionPushDeviceStore(
        PushDeviceManager pushDeviceManager,
        IIdentitySessionRepository sessionRepository)
        : base(pushDeviceManager)
    {
        SessionRepository = sessionRepository;
    }

    protected override async Task<bool> IsActiveAsync(PushDevice device, CancellationToken cancellationToken)
    {
        // Registered without a session (no session_id claim): nothing to judge by.
        return device.SessionId == null
            || await SessionRepository.ExistAsync(device.SessionId, cancellationToken);
    }
}
