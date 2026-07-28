using System;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

/// <summary>
/// The typed value slots of one query-index row. Exists so
/// <see cref="FlexFieldsDbContextModelCreatingExtensions.ConfigureFlexFieldIndex{TIndex}"/> can constrain
/// on a single type parameter that EF Core infers from the builder, the same way ABP's
/// <c>ConfigureAbpUser&lt;TUser&gt;</c> constrains on <c>IUser</c>.
/// <para>
/// Lives in the EF Core provider on purpose: a typed pivot table is this provider's answer to a problem
/// a document store does not have (it indexes the embedded value bag in place). Nothing relational is
/// welded into the abstraction layer.
/// </para>
/// </summary>
public interface IFlexFieldIndex
{
    Guid FieldId { get; set; }

    FlexFieldValueType ValueType { get; set; }

    string? StringValue { get; set; }

    decimal? NumberValue { get; set; }

    DateTime? DateTimeValue { get; set; }

    bool? BooleanValue { get; set; }

    Guid? GuidValue { get; set; }
}
