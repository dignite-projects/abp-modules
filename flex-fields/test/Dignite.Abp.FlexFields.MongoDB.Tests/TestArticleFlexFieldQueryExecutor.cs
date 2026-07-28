using Volo.Abp.DependencyInjection;
using Volo.Abp.MongoDB;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Downstream stand-in: adopting the kernel's query executor costs a constructor and no hook overrides at
/// all - the EF Core counterpart still has to name its index table's foreign key.
/// </summary>
public class TestArticleFlexFieldQueryExecutor
    : MongoFlexFieldQueryExecutorBase<ITestFlexFieldsMongoDbContext, TestArticle>,
      ITransientDependency
{
    public TestArticleFlexFieldQueryExecutor(IMongoDbContextProvider<ITestFlexFieldsMongoDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
