using Volo.Abp;
using Volo.Abp.Testing;

namespace Dignite.Abp.FlexFields;

public abstract class DigniteAbpFlexFieldsTestBase : AbpIntegratedTest<DigniteAbpFlexFieldsTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }
}
