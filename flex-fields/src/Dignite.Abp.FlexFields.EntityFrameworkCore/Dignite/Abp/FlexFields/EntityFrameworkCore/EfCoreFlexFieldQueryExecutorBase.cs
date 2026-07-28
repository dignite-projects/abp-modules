using System;
using System.Collections.Generic;
using System.Globalization;
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
        if (conditions == null || conditions.Count == 0)
        {
            throw new ArgumentException(
                "At least one condition is required - an unfiltered query is the host's own, not a flex field query.",
                nameof(conditions));
        }

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
        var value = bool.Parse(condition.Value);

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
            var values = SplitValues(condition.Value).Select(Guid.Parse).ToList();
            return query.Where(x => x.GuidValue.HasValue && values.Contains(x.GuidValue.Value));
        }

        var value = Guid.Parse(condition.Value);

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
    /// </summary>
    protected static decimal ParseNumber(string value)
    {
        return decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }

    protected static DateTime ParseDateTime(string value)
    {
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    protected static List<string> SplitValues(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static AbpException UnsupportedOperator(FlexFieldQueryCondition condition)
    {
        return new AbpException(
            $"The operator ({condition.Operator}) is not supported for value type ({condition.ValueType}) on field ({condition.FieldId}).");
    }
}
