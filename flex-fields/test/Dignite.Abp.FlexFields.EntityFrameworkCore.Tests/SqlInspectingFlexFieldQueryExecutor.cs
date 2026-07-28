using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// Test-only: exposes the SQL the base class composes for a single condition, so a test can assert the
/// filtering really is pushed down to the database rather than applied to materialized entities.
/// <para>
/// <see cref="ExposeServicesAttribute"/> pins conventional registration to this concrete type only.
/// Without it, ABP's default exposure rule (a class is auto-exposed as any interface whose name, minus
/// "I", it ends with) would ALSO expose this as <see cref="IFlexFieldQueryExecutor{TEntity}"/> - the same
/// interface <see cref="TestArticleFlexFieldQueryExecutor"/> registers as (its name matches too) - and the
/// two registrations would silently race for that resolution, picked by assembly type-enumeration order
/// rather than by either test. Resolve this one only by its own type, as <see cref="FlexFieldQueryPushdown_Tests"/> does.
/// </para>
/// </summary>
[ExposeServices(typeof(SqlInspectingFlexFieldQueryExecutor))]
public class SqlInspectingFlexFieldQueryExecutor
    : EfCoreFlexFieldQueryExecutorBase<ITestFlexFieldsDbContext, TestArticle, TestArticleFlexFieldIndex>,
      ITransientDependency
{
    protected override string EntityIdPropertyName => nameof(TestArticleFlexFieldIndex.ArticleId);

    public SqlInspectingFlexFieldQueryExecutor(IDbContextProvider<ITestFlexFieldsDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<string> GetConditionSqlAsync(FlexFieldQueryCondition condition)
    {
        var dbContext = await DbContextProvider.GetDbContextAsync();
        var query = ApplyCondition(dbContext.Set<TestArticleFlexFieldIndex>(), condition);
        return SelectEntityIds(query).Distinct().ToQueryString();
    }
}
