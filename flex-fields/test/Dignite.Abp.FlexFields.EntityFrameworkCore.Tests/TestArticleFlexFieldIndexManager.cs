using System;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Downstream stand-in: everything a real host has to write to adopt the kernel's index manager - two
/// hook overrides and a constructor. <c>ITransientDependency</c> is what exposes it as
/// <c>IFlexFieldIndexManager&lt;TestArticle&gt;</c>.
/// </summary>
public class TestArticleFlexFieldIndexManager
    : EfCoreFlexFieldIndexManagerBase<ITestFlexFieldsDbContext, TestArticle, TestArticleFlexFieldIndex>,
      ITransientDependency
{
    /// <summary>Two rows per page so rebuild tests cross page boundaries without seeding hundreds.</summary>
    protected override int RebuildPageSize => 2;

    protected override string EntityIdPropertyName => nameof(TestArticleFlexFieldIndex.ArticleId);

    public TestArticleFlexFieldIndexManager(
        IDbContextProvider<ITestFlexFieldsDbContext> dbContextProvider,
        IFlexFieldProvider<TestArticle> flexFieldProvider,
        IFieldTypeResolver fieldTypeResolver)
        : base(dbContextProvider, flexFieldProvider, fieldTypeResolver)
    {
    }

    protected override TestArticleFlexFieldIndex CreateIndexRow(Guid entityId, Guid fieldId, FlexFieldIndexValue value)
    {
        return new TestArticleFlexFieldIndex(Guid.NewGuid(), entityId, fieldId, value);
    }
}
