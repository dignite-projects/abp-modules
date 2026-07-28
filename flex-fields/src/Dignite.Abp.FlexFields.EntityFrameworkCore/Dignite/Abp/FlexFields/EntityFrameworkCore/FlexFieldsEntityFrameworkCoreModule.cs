using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Registers nothing - deliberately, like ABP's own <c>AbpUsersEntityFrameworkCoreModule</c>. This
/// package ships no <c>DbContext</c>, no <c>DbSet</c> and no repository: a downstream host owns its
/// <c>DbContext</c>, tables and migrations, and reaches the kernel only through the
/// <see cref="FlexFieldsDbContextModelCreatingExtensions"/> builder extensions and
/// <see cref="FlexFieldIndexBase{TEntity}"/>. The dependency declarations exist so
/// <c>FlexFieldConsts</c> and the field-type registrations are in the graph.
/// </summary>
[DependsOn(
    typeof(FlexFieldsDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
    )]
public class FlexFieldsEntityFrameworkCoreModule : AbpModule
{
}
