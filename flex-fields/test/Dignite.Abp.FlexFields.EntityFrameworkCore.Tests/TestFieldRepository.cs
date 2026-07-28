using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Downstream stand-in: everything a real host has to write to adopt the kernel's repository base - just
/// the constructor. The base's own <c>ITransientDependency</c> is enough for this to resolve as itself and
/// as the standard ABP repository interfaces; resolving as <see cref="IFlexFieldRepository{TField}"/>
/// itself additionally needs the explicit <c>AddTransient</c> registration in
/// <see cref="FlexFieldsEntityFrameworkCoreTestModule"/> (see
/// <see cref="EfCoreFlexFieldRepositoryBase{TDbContext,TField}"/> for why), and
/// <c>options.AddRepository&lt;TestField, TestFieldRepository&gt;()</c> is what routes the standard
/// <c>IRepository&lt;TestField, Guid&gt;</c> family to this class instead of a second, generic one.
/// </summary>
public class TestFieldRepository : EfCoreFlexFieldRepositoryBase<ITestFlexFieldsDbContext, TestField>
{
    public TestFieldRepository(IDbContextProvider<ITestFlexFieldsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
