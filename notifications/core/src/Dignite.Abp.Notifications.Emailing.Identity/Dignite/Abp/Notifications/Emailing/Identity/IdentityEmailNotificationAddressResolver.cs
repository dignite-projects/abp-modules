using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.Notifications.Emailing;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.SettingManagement;

namespace Dignite.Abp.Notifications.Emailing.Identity;

/// <summary>
/// Built-in fallback resolver: the recipient's ABP Identity account email.
/// </summary>
/// <remarks>
/// Sits at <see cref="NotificationEmailProviderOrders.BuiltInFallback"/>, so any application resolver at
/// <see cref="NotificationEmailProviderOrders.Default"/> gets to claim a notification first. It does not replace
/// services — an application resolver coexists with it rather than displacing it.
/// <para>
/// Queries Identity under the ambient tenant and never switches tenants itself. ABP's event bus enters the
/// delivery event's tenant before invoking the notifier — in-process and across a message broker alike.
/// Resolvers that reach a user directory outside this process (a remote user service) need the tenant on the wire
/// instead, which is why <see cref="EmailNotificationAddressResolveContext.TenantId"/> still carries it explicitly.
/// </para>
/// </remarks>
public class IdentityEmailNotificationAddressResolver : IEmailNotificationAddressResolver, ITransientDependency
{
    public int Order => NotificationEmailProviderOrders.BuiltInFallback;

    protected IIdentityUserRepository UserRepository { get; }

    /// <summary>
    /// Optional because the base Identity integration does not require ABP Setting Management. When it is installed,
    /// its user-setting manager provides the complete user → tenant → global → configuration/default fallback.
    /// </summary>
    protected ISettingManager? SettingManager { get; }

    public IdentityEmailNotificationAddressResolver(
        IIdentityUserRepository userRepository,
        ISettingManager? settingManager = null)
    {
        UserRepository = userRepository;
        SettingManager = settingManager;
    }

    public virtual async Task<EmailNotificationAddress?> GetEmailOrNullAsync(
        EmailNotificationAddressResolveContext context,
        CancellationToken cancellationToken = default)
    {
        var user = await UserRepository.FindAsync(context.UserId, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(user?.Email))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        // ISettingProvider reads the ambient user, which may differ from this delivery's recipient. Use ABP's
        // user-targeted setting-management API, which performs the complete fallback chain for this UserId.
        var cultureName = SettingManager == null
            ? null
            : await SettingManager.GetOrNullForUserAsync(
                LocalizationSettingNames.DefaultLanguage,
                context.UserId,
                fallback: true);
        cancellationToken.ThrowIfCancellationRequested();

        return EmailNotificationAddress.To(user.Email, cultureName);
    }
}
