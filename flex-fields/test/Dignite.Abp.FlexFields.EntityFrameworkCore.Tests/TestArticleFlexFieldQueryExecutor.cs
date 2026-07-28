using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Downstream stand-in: adopting the kernel's query executor costs one hook override.
/// </summary>
public class TestArticleFlexFieldQueryExecutor
    : EfCoreFlexFieldQueryExecutorBase<ITestFlexFieldsDbContext, TestArticle, TestArticleFlexFieldIndex>,
      ITransientDependency
{
    protected override string EntityIdPropertyName => nameof(TestArticleFlexFieldIndex.ArticleId);

    public TestArticleFlexFieldQueryExecutor(IDbContextProvider<ITestFlexFieldsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
