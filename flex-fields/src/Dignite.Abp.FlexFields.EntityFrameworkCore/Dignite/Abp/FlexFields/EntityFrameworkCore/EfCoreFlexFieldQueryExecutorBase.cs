using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// EF Core implementation of <see cref="IFlexFieldQueryExecutor{TEntity}"/>. Every comparison runs as a
/// real SQL predicate against the typed value column named by the condition's
/// <see cref="FlexFieldQueryCondition.ValueType"/>; each condition is added to the host query as its own
/// <c>WHERE Id IN (...)</c> subquery, so the whole filter - AND across conditions included - is one
/// composed query, resolved in a single round trip whenever the caller enumerates it.
/// <para>
/// Abstract and generic over the downstream's DbContext interface, host entity and index row type, so the
/// kernel never names a concrete DbContext or table. A subclass supplies only the name of its own
/// foreign-key property.
/// </para>
/// <para>
/// Requires the query passed to <see cref="ApplyFilterAsync"/> to come from the same DbContext instance
/// as <typeparamref name="TIndex"/> - guaranteed here because the downstream's own DbContext both hosts
/// <typeparamref name="TEntity"/> and implements <typeparamref name="TDbContext"/>, so ABP's unit-of-work
/// resolves both to the same instance. EF Core cannot compose a query across two separate DbContext
/// instances.
/// </para>
/// </summary>
public abstract class EfCoreFlexFieldQueryExecutorBase<TDbContext, TEntity, TIndex> : IFlexFieldQueryExecutor<TEntity>
    where TDbContext : IEfCoreDbContext
    where TEntity : class, IHasFlexFields, IEntity<Guid>
    where TIndex : class, IFlexFieldIndex
{
    protected IDbContextProvider<TDbContext> DbContextProvider { get; }

    /// <summary>
    /// Name of the downstream's own foreign-key property on <typeparamref name="TIndex"/>. Supply it with
    /// <c>nameof</c>.
    /// </summary>
    protected abstract string EntityIdPropertyName { get; }

    protected EfCoreFlexFieldQueryExecutorBase(IDbContextProvider<TDbContext> dbContextProvider)
    {
        DbContextProvider = dbContextProvider;
    }

    public virtual async Task<IQueryable<TEntity>> ApplyFilterAsync(
        IQueryable<TEntity> query,
        IReadOnlyList<FlexFieldQueryCondition> conditions,
        CancellationToken cancellationToken = default)
    {
        FlexFieldQueryConditions.EnsureNotEmpty(conditions);

        var dbContext = await DbContextProvider.GetDbContextAsync();
        var indexSet = dbContext.Set<TIndex>();

        foreach (var condition in conditions)
        {
            var matchingIds = SelectEntityIds(ApplyCondition(indexSet, condition));
            query = query.Where(e => matchingIds.Contains(e.Id));
        }

        return query;
    }

    /// <summary>
    /// Projects the downstream's foreign key. Hoisted to a local because EF Core needs to fold the
    /// property name to a constant when translating.
    /// </summary>
    protected virtual IQueryable<Guid> SelectEntityIds(IQueryable<TIndex> query)
    {
        var entityIdProperty = EntityIdPropertyName;
        return query.Select(x => EF.Property<Guid>(x, entityIdProperty));
    }

    protected virtual IQueryable<TIndex> ApplyCondition(IQueryable<TIndex> query, FlexFieldQueryCondition condition)
    {
        // Which comparisons a value type admits is the module's answer, not this provider's - see
        // FlexFieldQueryConditions. Checked up front so the per-type branches below never have to be the
        // place a caller finds out.
        FlexFieldQueryConditions.EnsureOperatorSupported(condition);

        query = query.Where(x => x.FieldId == condition.FieldId && x.ValueType == condition.ValueType);

        return condition.ValueType switch
        {
            FlexFieldValueType.String => ApplyStringCondition(query, condition),
            FlexFieldValueType.Number => ApplyNumberCondition(query, condition),
            FlexFieldValueType.DateTime => ApplyDateTimeCondition(query, condition),
            FlexFieldValueType.Boolean => ApplyBooleanCondition(query, condition),
            FlexFieldValueType.Guid => ApplyGuidCondition(query, condition),
            _ => throw new AbpException($"Unknown {nameof(FlexFieldValueType)} ({condition.ValueType}).")
        };
    }

    protected virtual IQueryable<TIndex> ApplyStringCondition(IQueryable<TIndex> query, FlexFieldQueryCondition condition)
    {
        if (condition.Operator == FlexFieldQueryOperator.In)
        {
            var values = SplitValues(condition.Value);
            return query.Where(x => x.StringValue != null && values.Contains(x.StringValue));
        }

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => query.Where(x => x.StringValue == condition.Value),
            FlexFieldQueryOperator.NotEquals => query.Where(x => x.StringValue != condition.Value),
            FlexFieldQueryOperator.Contains => query.Where(x => x.StringValue != null && x.StringValue.Contains(condition.Value)),
            _ => throw UnsupportedOperator(condition)
        };
    }

    protected virtual IQueryable<TIndex> ApplyNumberCondition(IQueryable<TIndex> query, FlexFieldQueryCondition condition)
    {
        if (condition.Operator == FlexFieldQueryOperator.In)
        {
            var values = SplitValues(condition.Value).Select(ParseNumber).ToList();
            return query.Where(x => x.NumberValue.HasValue && values.Contains(x.NumberValue.Value));
        }

        var value = ParseNumber(condition.Value);

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => query.Where(x => x.NumberValue == value),
            FlexFieldQueryOperator.NotEquals => query.Where(x => x.NumberValue != value),
            FlexFieldQueryOperator.GreaterThan => query.Where(x => x.NumberValue > value),
            FlexFieldQueryOperator.GreaterThanOrEqual => query.Where(x => x.NumberValue >= value),
            FlexFieldQueryOperator.LessThan => query.Where(x => x.NumberValue < value),
            FlexFieldQueryOperator.LessThanOrEqual => query.Where(x => x.NumberValue <= value),
            _ => throw UnsupportedOperator(condition)
        };
    }

    protected virtual IQueryable<TIndex> ApplyDateTimeCondition(IQueryable<TIndex> query, FlexFieldQueryCondition condition)
    {
        if (condition.Operator == FlexFieldQueryOperator.In)
        {
            var values = SplitValues(condition.Value).Select(ParseDateTime).ToList();
            return query.Where(x => x.DateTimeValue.HasValue && values.Contains(x.DateTimeValue.Value));
        }

        var value = ParseDateTime(condition.Value);

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => query.Where(x => x.DateTimeValue == value),
            FlexFieldQueryOperator.NotEquals => query.Where(x => x.DateTimeValue != value),
            FlexFieldQueryOperator.GreaterThan => query.Where(x => x.DateTimeValue > value),
            FlexFieldQueryOperator.GreaterThanOrEqual => query.Where(x => x.DateTimeValue >= value),
            FlexFieldQueryOperator.LessThan => query.Where(x => x.DateTimeValue < value),
            FlexFieldQueryOperator.LessThanOrEqual => query.Where(x => x.DateTimeValue <= value),
            _ => throw UnsupportedOperator(condition)
        };
    }

    protected virtual IQueryable<TIndex> ApplyBooleanCondition(IQueryable<TIndex> query, FlexFieldQueryCondition condition)
    {
        var value = (bool)FlexFieldValueConverter.Parse(FlexFieldValueType.Boolean, condition.Value);

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => query.Where(x => x.BooleanValue == value),
            FlexFieldQueryOperator.NotEquals => query.Where(x => x.BooleanValue != value),
            _ => throw UnsupportedOperator(condition)
        };
    }

    protected virtual IQueryable<TIndex> ApplyGuidCondition(IQueryable<TIndex> query, FlexFieldQueryCondition condition)
    {
        if (condition.Operator == FlexFieldQueryOperator.In)
        {
            var values = FlexFieldValueConverter.ParseList(FlexFieldValueType.Guid, condition.Value)
                .Cast<Guid>().ToList();
            return query.Where(x => x.GuidValue.HasValue && values.Contains(x.GuidValue.Value));
        }

        var value = (Guid)FlexFieldValueConverter.Parse(FlexFieldValueType.Guid, condition.Value);

        return condition.Operator switch
        {
            FlexFieldQueryOperator.Equals => query.Where(x => x.GuidValue == value),
            FlexFieldQueryOperator.NotEquals => query.Where(x => x.GuidValue != value),
            _ => throw UnsupportedOperator(condition)
        };
    }

    /// <summary>
    /// Invariant culture throughout: a condition's <see cref="FlexFieldQueryCondition.Value"/> is transport data,
    /// so it must not be parsed against whatever culture the request happens to run under.
    /// <para>
    /// These delegate to <see cref="FlexFieldValueConverter"/>, which is also what wrote the indexed value
    /// (<see cref="FlexFieldIndexValue.Create"/>). Sharing one implementation is what keeps the two sides
    /// from reading the same text differently; they are kept here as named entry points because a
    /// downstream subclass overriding an <c>Apply*Condition</c> needs them.
    /// </para>
    /// </summary>
    protected static decimal ParseNumber(string value)
    {
        return (decimal)FlexFieldValueConverter.Parse(FlexFieldValueType.Number, value);
    }

    protected static DateTime ParseDateTime(string value)
    {
        return (DateTime)FlexFieldValueConverter.Parse(FlexFieldValueType.DateTime, value);
    }

    protected static List<string> SplitValues(string value)
    {
        return FlexFieldValueConverter.SplitValues(value);
    }

    /// <summary>
    /// Defensive: <see cref="ApplyCondition"/> has already rejected an unsupported operator, so these are
    /// the unreachable arms a switch expression still has to have. The wording is the shared one either way.
    /// </summary>
    private static AbpException UnsupportedOperator(FlexFieldQueryCondition condition)
    {
        return FlexFieldQueryConditions.UnsupportedOperator(condition);
    }
}
