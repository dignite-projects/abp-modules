using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

public abstract class FlexFieldsEntityFrameworkCoreTestBase : AbpIntegratedTest<FlexFieldsEntityFrameworkCoreTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action)
    {
        using var uow = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false);
        await action();
        await uow.CompleteAsync();
    }
}
