using System;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// <see cref="FlexFieldIndexValue.Create"/>: the conversion from a raw searchable value
/// (<see cref="IFieldType.GetSearchableValues"/>) into the typed slot named by a
/// <see cref="FlexFieldValueType"/>. No ABP host needed - the factory is the whole subject here.
/// </summary>
public class FlexFieldIndexValue_Tests
{
    [Fact]
    public void String_slot_stores_the_value_verbatim()
    {
        var value = FlexFieldIndexValue.Create(FlexFieldValueType.String, "hello");

        value.ValueType.ShouldBe(FlexFieldValueType.String);
        value.StringValue.ShouldBe("hello");
        value.NumberValue.ShouldBeNull();
    }

    [Fact]
    public void Number_slot_converts_an_int_to_decimal()
    {
        var value = FlexFieldIndexValue.Create(FlexFieldValueType.Number, 42);

        value.ValueType.ShouldBe(FlexFieldValueType.Number);
        value.NumberValue.ShouldBe(42m);
        value.StringValue.ShouldBeNull();
    }

    [Fact]
    public void DateTime_slot_converts_via_Convert_ToDateTime()
    {
        var dateValue = new DateTime(2025, 6, 1, 13, 45, 0);

        var value = FlexFieldIndexValue.Create(FlexFieldValueType.DateTime, dateValue);

        value.ValueType.ShouldBe(FlexFieldValueType.DateTime);
        value.DateTimeValue.ShouldBe(dateValue);
    }

    [Fact]
    public void Boolean_slot_converts_via_Convert_ToBoolean()
    {
        var value = FlexFieldIndexValue.Create(FlexFieldValueType.Boolean, true);

        value.ValueType.ShouldBe(FlexFieldValueType.Boolean);
        value.BooleanValue.ShouldBe(true);
    }

    [Fact]
    public void Guid_slot_accepts_a_Guid_value_directly()
    {
        var guidValue = Guid.NewGuid();

        var value = FlexFieldIndexValue.Create(FlexFieldValueType.Guid, guidValue);

        value.ValueType.ShouldBe(FlexFieldValueType.Guid);
        value.GuidValue.ShouldBe(guidValue);
    }

    [Fact]
    public void Guid_slot_parses_a_string_value()
    {
        var guidValue = Guid.NewGuid();

        var value = FlexFieldIndexValue.Create(FlexFieldValueType.Guid, guidValue.ToString());

        value.GuidValue.ShouldBe(guidValue);
    }

    [Fact]
    public void Unsupported_value_type_throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => FlexFieldIndexValue.Create((FlexFieldValueType)(-1), "irrelevant"));
    }
}
