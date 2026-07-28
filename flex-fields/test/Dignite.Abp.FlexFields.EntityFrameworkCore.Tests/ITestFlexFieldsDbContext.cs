using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// The test double's own DbContext interface, as a downstream would declare it (e.g. a CMS's
/// <c>ICmsDbContext</c>). The kernel ships no DbContext interface of its own - that is the whole point
/// of "support, not ownership".
/// </summary>
public interface ITestFlexFieldsDbContext : IEfCoreDbContext
{
    DbSet<TestArticle> Articles { get; }

    DbSet<TestField> Fields { get; }

    DbSet<TestArticleFlexFieldIndex> ArticleFlexFieldIndexes { get; }
}
