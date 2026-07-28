using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// <see cref="FlexFieldValueConverter"/> is the single definition of what a <see cref="FlexFieldValueType"/>
/// means as a CLR value, shared by whatever writes a derived/indexed value and whatever reads a
/// <see cref="FlexFieldQueryCondition"/>. No ABP host needed - the converter is the whole subject.
/// </summary>
public class FlexFieldValueConverter_Tests
{
    /// <summary>A culture whose decimal separator is ',' and whose group separator is '.'.</summary>
    private const string CommaDecimalCulture = "de-DE";

    public static IEnumerable<object[]> RoundTrips()
    {
        yield return new object[] { FlexFieldValueType.String, "hello", "hello" };
        yield return new object[] { FlexFieldValueType.Number, 42.5m, "42.5" };
        yield return new object[] { FlexFieldValueType.DateTime, new DateTime(2025, 6, 1, 13, 45, 0), "2025-06-01T13:45:00" };
        yield return new object[] { FlexFieldValueType.Boolean, true, "True" };
        yield return new object[] { FlexFieldValueType.Guid, Guid.Parse("6f9619ff-8b86-d011-b42d-00cf4fc964ff"), "6f9619ff-8b86-d011-b42d-00cf4fc964ff" };
    }

    /// <summary>
    /// The load-bearing invariant. A stored value is written through <see cref="FlexFieldValueConverter.Coerce"/>
    /// and searched for through <see cref="FlexFieldValueConverter.Parse"/>; if those two ever disagree on the
    /// same value, a value that was indexed can no longer be found. Asserted under a culture that would break
    /// an ambient-culture reading, because agreeing under the default culture proves nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(RoundTrips))]
    public void Coerce_and_Parse_agree_on_the_same_value(FlexFieldValueType valueType, object raw, string text)
    {
        using (UseCulture(CommaDecimalCulture))
        {
            FlexFieldValueConverter.Coerce(valueType, raw)
                .ShouldBe(FlexFieldValueConverter.Parse(valueType, text));
        }
    }

    [Theory]
    [MemberData(nameof(RoundTrips))]
    public void Coerce_returns_the_clr_type_the_value_type_names(FlexFieldValueType valueType, object raw, string text)
    {
        FlexFieldValueConverter.Coerce(valueType, raw).ShouldBeOfType(raw.GetType());
        FlexFieldValueConverter.Parse(valueType, text).ShouldBeOfType(raw.GetType());
    }

    [Fact]
    public void A_decimal_string_reads_the_same_under_any_culture()
    {
        using (UseCulture(CommaDecimalCulture))
        {
            // Under de-DE an ambient-culture read treats '.' as a group separator and yields 425.
            FlexFieldValueConverter.Coerce(FlexFieldValueType.Number, "42.5").ShouldBe(42.5m);
        }
    }

    [Fact]
    public void A_number_is_rendered_into_the_string_slot_by_invariant_culture()
    {
        using (UseCulture(CommaDecimalCulture))
        {
            // A field whose type changed from Number to Text re-reads the same bag value into the string
            // slot; an ambient-culture render would write "42,5" and no invariant condition would match it.
            FlexFieldValueConverter.Coerce(FlexFieldValueType.String, 42.5m).ShouldBe("42.5");
        }
    }

    [Theory]
    [InlineData(FlexFieldValueType.String, "\"hello\"", "hello")]
    [InlineData(FlexFieldValueType.Number, "42.5", 42.5)]
    [InlineData(FlexFieldValueType.Boolean, "true", true)]
    public void A_JsonElement_left_by_a_json_round_trip_is_unwrapped(
        FlexFieldValueType valueType,
        string json,
        object expected)
    {
        // A value bag is a Dictionary<string, object>, so a deserializer that does not infer types leaves
        // JsonElements behind - most visibly on the inbound DTO path.
        var element = JsonDocument.Parse(json).RootElement.Clone();

        var value = FlexFieldValueConverter.Coerce(valueType, element);

        value.ShouldBe(Convert.ChangeType(expected, value.GetType(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void A_JsonElement_holding_a_guid_is_unwrapped()
    {
        var guid = Guid.NewGuid();
        var element = JsonDocument.Parse($"\"{guid}\"").RootElement.Clone();

        FlexFieldValueConverter.Coerce(FlexFieldValueType.Guid, element).ShouldBe(guid);
    }

    [Fact]
    public void An_unsupported_value_type_throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => FlexFieldValueConverter.Coerce((FlexFieldValueType)(-1), "irrelevant"));
    }

    [Fact]
    public void A_null_raw_value_throws()
    {
        Should.Throw<ArgumentNullException>(
            () => FlexFieldValueConverter.Coerce(FlexFieldValueType.String, null!));
    }

    [Fact]
    public void SplitValues_trims_members_and_drops_empty_ones()
    {
        FlexFieldValueConverter.SplitValues("red, blue ,,green,").ShouldBe(new[] { "red", "blue", "green" });
    }

    [Fact]
    public void ParseList_reads_every_member_as_the_named_type()
    {
        using (UseCulture(CommaDecimalCulture))
        {
            FlexFieldValueConverter.ParseList(FlexFieldValueType.Number, "1.5, 2.25")
                .ShouldBe(new object[] { 1.5m, 2.25m });
        }
    }

    private static IDisposable UseCulture(string name)
    {
        return new CultureScope(name);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo? _previousDefaultCulture;

        public CultureScope(string name)
        {
            var culture = CultureInfo.GetCultureInfo(name);

            _previousCulture = CultureInfo.CurrentCulture;
            _previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;

            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.DefaultThreadCurrentCulture = _previousDefaultCulture;
        }
    }
}
