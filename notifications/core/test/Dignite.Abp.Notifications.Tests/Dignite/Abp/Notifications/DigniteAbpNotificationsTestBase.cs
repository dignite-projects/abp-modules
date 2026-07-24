using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Testing;

namespace Dignite.Abp.Notifications;

public abstract class DigniteAbpNotificationsTestBase : AbpIntegratedTest<DigniteAbpNotificationsTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }
}
