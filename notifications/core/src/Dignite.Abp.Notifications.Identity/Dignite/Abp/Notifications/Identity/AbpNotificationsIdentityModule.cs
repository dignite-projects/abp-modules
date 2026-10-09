using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.Identity;

/// <summary>
/// Depends on the <c>IUserRoleFinder</c> abstraction (<c>Volo.Abp.Identity.Domain.Shared</c>), not on the Identity
/// domain: the host supplies the implementation — <c>UserRoleFinder</c> from <c>Volo.Abp.Identity.Domain</c> in a
/// process that owns the Identity database, <c>HttpClientUserRoleFinder</c> from an Identity <c>HttpApi.Client</c>
/// package in one that does not.
/// </summary>
[DependsOn(
    typeof(AbpNotificationsAbstractionsModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpIdentityDomainSharedModule)
    )]
public class AbpNotificationsIdentityModule : AbpModule
{
}
