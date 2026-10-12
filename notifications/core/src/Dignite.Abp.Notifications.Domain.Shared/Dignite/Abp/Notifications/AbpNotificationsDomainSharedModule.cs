using Volo.Abp.EventBus.Abstractions;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications;

/// <summary>
/// The shared layer of the definition store, after ABP's <c>AbpPermissionManagementDomainSharedModule</c>: the record
/// column sizes (<see cref="NotificationDefinitionRecordConsts"/>, <see cref="NotificationGroupDefinitionRecordConsts"/>)
/// and the event contracts other services consume.
/// </summary>
[DependsOn(typeof(AbpEventBusAbstractionsModule))]
public class AbpNotificationsDomainSharedModule : AbpModule
{
}
