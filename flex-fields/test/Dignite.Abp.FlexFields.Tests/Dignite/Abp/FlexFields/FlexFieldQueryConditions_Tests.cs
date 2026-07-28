using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// <see cref="FlexFieldQueryConditions"/>: which comparisons a value type admits, stated once for every
/// provider. The expected table below is written out independently of the production one on purpose - a test
/// that read the same table it is checking would assert nothing.
/// </summary>
public class FlexFieldQueryConditions_Tests
{
    private static readonly IReadOnlyDictionary<FlexFieldValueType, FlexFieldQueryOperator[]> Expected =
        new Dictionary<FlexFieldValueType, FlexFieldQueryOperator[]>
        {
            [FlexFieldValueType.String] = new[]
            {
                FlexFieldQueryOperator.Equals, FlexFieldQueryOperator.NotEquals,
                FlexFieldQueryOperator.Contains, FlexFieldQueryOperator.In
            },
            [FlexFieldValueType.Number] = new[]
            {
                FlexFieldQueryOperator.Equals, FlexFieldQueryOperator.NotEquals,
                FlexFieldQueryOperator.GreaterThan, FlexFieldQueryOperator.GreaterThanOrEqual,
                FlexFieldQueryOperator.LessThan, FlexFieldQueryOperator.LessThanOrEqual,
                FlexFieldQueryOperator.In
            },
            [FlexFieldValueType.DateTime] = new[]
            {
                FlexFieldQueryOperator.Equals, FlexFieldQueryOperator.NotEquals,
                FlexFieldQueryOperator.GreaterThan, FlexFieldQueryOperator.GreaterThanOrEqual,
                FlexFieldQueryOperator.LessThan, FlexFieldQueryOperator.LessThanOrEqual,
                FlexFieldQueryOperator.In
            },
            [FlexFieldValueType.Boolean] = new[]
            {
                FlexFieldQueryOperator.Equals, FlexFieldQueryOperator.NotEquals
            },
            [FlexFieldValueType.Guid] = new[]
            {
                FlexFieldQueryOperator.Equals, FlexFieldQueryOperator.NotEquals, FlexFieldQueryOperator.In
            }
        };

    public static IEnumerable<object[]> SupportedPairs() => Pairs(supported: true);

    public static IEnumerable<object[]> UnsupportedPairs() => Pairs(supported: false);

    private static IEnumerable<object[]> Pairs(bool supported)
    {
        foreach (var valueType in Enum.GetValues<FlexFieldValueType>())
        {
            foreach (var @operator in Enum.GetValues<FlexFieldQueryOperator>())
            {
                if (Expected[valueType].Contains(@operator) == supported)
                {
                    yield return new object[] { valueType, @operator };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(SupportedPairs))]
    public void Supported_pairs_are_accepted(FlexFieldValueType valueType, FlexFieldQueryOperator @operator)
    {
        FlexFieldQueryConditions.IsOperatorSupported(valueType, @operator).ShouldBeTrue();
        Should.NotThrow(() => FlexFieldQueryConditions.EnsureOperatorSupported(
            new FlexFieldQueryCondition(Guid.NewGuid(), @operator, "irrelevant", valueType)));
    }

    [Theory]
    [MemberData(nameof(UnsupportedPairs))]
    public void Unsupported_pairs_are_rejected(FlexFieldValueType valueType, FlexFieldQueryOperator @operator)
    {
        FlexFieldQueryConditions.IsOperatorSupported(valueType, @operator).ShouldBeFalse();
        Should.Throw<AbpException>(() => FlexFieldQueryConditions.EnsureOperatorSupported(
            new FlexFieldQueryCondition(Guid.NewGuid(), @operator, "irrelevant", valueType)));
    }

    [Fact]
    public void An_unknown_value_type_supports_nothing()
    {
        FlexFieldQueryConditions
            .IsOperatorSupported((FlexFieldValueType)(-1), FlexFieldQueryOperator.Equals)
            .ShouldBeFalse();
    }

    [Fact]
    public void The_rejection_names_the_operator_the_value_type_and_the_field()
    {
        var fieldId = Guid.NewGuid();

        var exception = Should.Throw<AbpException>(() => FlexFieldQueryConditions.EnsureOperatorSupported(
            new FlexFieldQueryCondition(fieldId, FlexFieldQueryOperator.In, "true,false", FlexFieldValueType.Boolean)));

        exception.Message.ShouldContain(nameof(FlexFieldQueryOperator.In));
        exception.Message.ShouldContain(nameof(FlexFieldValueType.Boolean));
        exception.Message.ShouldContain(fieldId.ToString());
    }

    [Fact]
    public void An_empty_condition_set_is_rejected()
    {
        Should.Throw<ArgumentException>(() => FlexFieldQueryConditions.EnsureNotEmpty(Array.Empty<FlexFieldQueryCondition>()));
        Should.Throw<ArgumentException>(() => FlexFieldQueryConditions.EnsureNotEmpty(null));
    }

    [Fact]
    public void A_single_condition_is_enough()
    {
        Should.NotThrow(() => FlexFieldQueryConditions.EnsureNotEmpty(new[]
        {
            new FlexFieldQueryCondition(Guid.NewGuid(), FlexFieldQueryOperator.Equals, "x", FlexFieldValueType.String)
        }));
    }
}
