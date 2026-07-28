using System;
using System.Collections.Generic;
using Volo.Abp;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// What a set of <see cref="FlexFieldQueryCondition"/>s is allowed to say, defined once for every
/// persistence provider. A provider translates conditions into its own query language; which conditions are
/// even meaningful is not its decision to make, and two providers quietly disagreeing about it is the same
/// module answering the same question two ways.
/// </summary>
public static class FlexFieldQueryConditions
{
    /// <summary>
    /// Operators each <see cref="FlexFieldValueType"/> can be compared with. Ordering operators are limited
    /// to the types that have a natural order; <see cref="FlexFieldQueryOperator.Contains"/> is substring
    /// matching and so is text-only; <see cref="FlexFieldQueryOperator.In"/> is set membership and so is
    /// pointless for a two-valued type.
    /// </summary>
    private static readonly IReadOnlyDictionary<FlexFieldValueType, FlexFieldQueryOperator[]> SupportedOperators =
        new Dictionary<FlexFieldValueType, FlexFieldQueryOperator[]>
        {
            [FlexFieldValueType.String] = new[]
            {
                FlexFieldQueryOperator.Equals,
                FlexFieldQueryOperator.NotEquals,
                FlexFieldQueryOperator.Contains,
                FlexFieldQueryOperator.In
            },
            [FlexFieldValueType.Number] = new[]
            {
                FlexFieldQueryOperator.Equals,
                FlexFieldQueryOperator.NotEquals,
                FlexFieldQueryOperator.GreaterThan,
                FlexFieldQueryOperator.GreaterThanOrEqual,
                FlexFieldQueryOperator.LessThan,
                FlexFieldQueryOperator.LessThanOrEqual,
                FlexFieldQueryOperator.In
            },
            [FlexFieldValueType.DateTime] = new[]
            {
                FlexFieldQueryOperator.Equals,
                FlexFieldQueryOperator.NotEquals,
                FlexFieldQueryOperator.GreaterThan,
                FlexFieldQueryOperator.GreaterThanOrEqual,
                FlexFieldQueryOperator.LessThan,
                FlexFieldQueryOperator.LessThanOrEqual,
                FlexFieldQueryOperator.In
            },
            [FlexFieldValueType.Boolean] = new[]
            {
                FlexFieldQueryOperator.Equals,
                FlexFieldQueryOperator.NotEquals
            },
            [FlexFieldValueType.Guid] = new[]
            {
                FlexFieldQueryOperator.Equals,
                FlexFieldQueryOperator.NotEquals,
                FlexFieldQueryOperator.In
            }
        };

    /// <summary>
    /// Rejects an empty condition set rather than reading it as "match everything" - that is the host's own
    /// unfiltered query, not a flex field query.
    /// </summary>
    public static void EnsureNotEmpty(IReadOnlyList<FlexFieldQueryCondition>? conditions)
    {
        if (conditions == null || conditions.Count == 0)
        {
            throw new ArgumentException(
                "At least one condition is required - an unfiltered query is the host's own, not a flex field query.",
                nameof(conditions));
        }
    }

    public static bool IsOperatorSupported(FlexFieldValueType valueType, FlexFieldQueryOperator @operator)
    {
        return SupportedOperators.TryGetValue(valueType, out var operators) &&
               Array.IndexOf(operators, @operator) >= 0;
    }

    public static void EnsureOperatorSupported(FlexFieldQueryCondition condition)
    {
        if (!IsOperatorSupported(condition.ValueType, condition.Operator))
        {
            throw UnsupportedOperator(condition);
        }
    }

    /// <summary>
    /// The one wording of "that comparison is not available on that kind of value", so a caller sees the same
    /// message whichever provider is installed.
    /// </summary>
    public static AbpException UnsupportedOperator(FlexFieldQueryCondition condition)
    {
        return new AbpException(
            $"The operator ({condition.Operator}) is not supported for value type ({condition.ValueType}) on field ({condition.FieldId}).");
    }
}
