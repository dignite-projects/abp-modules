using Dignite.Abp.FlexFields.Demo.Data;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.Demo.Services.FlexFields;

/// <summary>
/// <see cref="IFlexFieldRepository{TField}"/> for <see cref="ProductField"/>. The base class's own
/// <c>ITransientDependency</c> is enough to resolve this as itself and as the standard ABP repository
/// interfaces; resolving as <c>IFlexFieldRepository&lt;ProductField&gt;</c> itself additionally needs the
/// explicit <c>AddTransient</c> registration in <c>DemoModule.ConfigureEfCore</c> - see
/// <see cref="EfCoreFlexFieldRepositoryBase{TDbContext,TField}"/>'s remarks for why.
/// </summary>
public class ProductFieldRepository : EfCoreFlexFieldRepositoryBase<IDemoDbContext, ProductField>
{
    public ProductFieldRepository(IDbContextProvider<IDemoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
