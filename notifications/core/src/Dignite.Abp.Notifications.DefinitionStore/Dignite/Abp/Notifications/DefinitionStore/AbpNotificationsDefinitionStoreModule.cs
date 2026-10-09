using Volo.Abp.Domain;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// The notification definition store, after ABP's <c>AbpPermissionManagementDomainModule</c>: definition records shared
/// by every process that installs it.
/// </summary>
[DependsOn(
    typeof(AbpNotificationsModule),
    typeof(AbpDddDomainModule)
    )]
public class AbpNotificationsDefinitionStoreModule : AbpModule
{
}
