using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MongoDB;
using Volo.Abp.Uow;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Downstream stand-in: everything a real host has to write to adopt the kernel's index manager - a
/// constructor, and nothing else. The EF Core counterpart additionally has to supply its foreign-key
/// property name and a row factory, because it owns a pivot table; there is no pivot table here.
/// <c>ITransientDependency</c> is what exposes it as <c>IFlexFieldIndexManager&lt;TestArticle&gt;</c>.
/// </summary>
public class TestArticleFlexFieldIndexManager
    : MongoFlexFieldIndexManagerBase<ITestFlexFieldsMongoDbContext, TestArticle>,
      ITransientDependency
{
    /// <summary>Two rows per page so rebuild tests cross page boundaries without seeding hundreds.</summary>
    protected override int RebuildPageSize => 2;

    public TestArticleFlexFieldIndexManager(
        IMongoDbContextProvider<ITestFlexFieldsMongoDbContext> dbContextProvider,
        IFlexFieldProvider<TestArticle> flexFieldProvider,
        IBasicRepository<TestArticle> repository,
        IFieldTypeResolver fieldTypeResolver,
        IUnitOfWorkManager unitOfWorkManager)
        : base(dbContextProvider, flexFieldProvider, repository, fieldTypeResolver, unitOfWorkManager)
    {
    }
}
