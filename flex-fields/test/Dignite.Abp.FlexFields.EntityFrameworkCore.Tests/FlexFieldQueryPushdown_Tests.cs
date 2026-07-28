using System;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Numeric;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// The design's central claim: querying by field is a database predicate on a typed column, not a filter
/// applied to already-materialized host entities. Asserted against the generated SQL, because a query
/// that silently degraded to client evaluation would still return the right answers in these tests -
/// only slowly, and only in production.
/// </summary>
public class FlexFieldQueryPushdown_Tests : FlexFieldsEntityFrameworkCoreTestBase
{
    private SqlInspectingFlexFieldQueryExecutor Executor =>
        GetRequiredService<SqlInspectingFlexFieldQueryExecutor>();

    [Fact]
    public async Task String_condition_becomes_sql_against_the_string_column()
    {
        var sql = await SqlForAsync(new FlexFieldQueryCondition(
            Guid.NewGuid(), FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String));

        sql.ShouldContain("SELECT");
        sql.ShouldContain("WHERE");
        sql.ShouldContain(nameof(IFlexFieldIndex.StringValue));
        sql.ShouldContain(nameof(TestArticleFlexFieldIndex.ArticleId));
        sql.ShouldContain("TestArticleFlexFieldIndexes");
        // The host table is never touched - no join back to the entity, no entity materialization.
        sql.ShouldNotContain("TestArticles");
    }

    [Fact]
    public async Task Number_range_condition_becomes_sql_against_the_number_column()
    {
        var sql = await SqlForAsync(new FlexFieldQueryCondition(
            Guid.NewGuid(), FlexFieldQueryOperator.GreaterThanOrEqual, "15", FlexFieldValueType.Number));

        sql.ShouldContain(nameof(IFlexFieldIndex.NumberValue));
        sql.ShouldContain(">=");
        // Comparison happens on the typed numeric column, never on the string slot.
        sql.ShouldNotContain(nameof(IFlexFieldIndex.StringValue));
    }

    [Fact]
    public async Task In_condition_becomes_a_sql_set_membership_test()
    {
        var sql = await SqlForAsync(new FlexFieldQueryCondition(
            Guid.NewGuid(), FlexFieldQueryOperator.In, "red,blue", FlexFieldValueType.String));

        sql.ShouldContain(nameof(IFlexFieldIndex.StringValue));
        sql.ShouldContain("IN");
    }

    [Fact]
    public async Task Condition_is_scoped_to_its_field_and_value_type()
    {
        var sql = await SqlForAsync(new FlexFieldQueryCondition(
            Guid.NewGuid(), FlexFieldQueryOperator.Equals, "Alpha", FlexFieldValueType.String));

        // Both discriminators are in the predicate, so one field's rows can never satisfy another's
        // condition and a stale row from a previous value type is excluded.
        sql.ShouldContain(nameof(IFlexFieldIndex.FieldId));
        sql.ShouldContain(nameof(IFlexFieldIndex.ValueType));
    }

    private async Task<string> SqlForAsync(FlexFieldQueryCondition condition)
    {
        string sql = null!;
        await WithUnitOfWorkAsync(async () => sql = await Executor.GetConditionSqlAsync(condition));
        return sql;
    }
}
