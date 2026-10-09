using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Kept for the modules that depend on it while the package layout changes; everything it held is now in
/// <see cref="AbpNotificationsAbstractionsModule"/>.
/// </summary>
[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class AbpNotificationsModule : AbpModule
{
}
