using Volo.Abp.MongoDB;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Downstream stand-in: everything a real host has to write to adopt the kernel's repository base - just
/// the constructor. Resolving as <see cref="IFlexFieldRepository{TField}"/> itself additionally needs the
/// explicit <c>AddTransient</c> registration in <see cref="FlexFieldsMongoDbTestModule"/> (see
/// <see cref="MongoFlexFieldRepositoryBase{TMongoDbContext,TField}"/> for why).
/// </summary>
public class TestFieldRepository : MongoFlexFieldRepositoryBase<ITestFlexFieldsMongoDbContext, TestField>
{
    public TestFieldRepository(IMongoDbContextProvider<ITestFlexFieldsMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
