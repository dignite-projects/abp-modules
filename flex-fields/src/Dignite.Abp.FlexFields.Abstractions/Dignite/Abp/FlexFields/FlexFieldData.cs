using System;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Plain-data implementation of <see cref="IFlexFieldData"/> - the type a downstream host's own
/// <c>IFlexField</c> entity (<c>Dignite.Abp.FlexFields.Domain</c>) is converted to at the Abstractions
/// boundary (see <c>FlexFieldExtensions.ToFlexFieldData()</c>), and the shape a future
/// <c>FlexFieldEto : IFlexFieldData</c> would carry across the event bus. The same role ABP's
/// <c>UserData</c> plays for <c>IUser</c>.
/// </summary>
public class FlexFieldData : IFlexFieldData
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }

    public string FieldTypeName { get; set; } = default!;

    public FieldConfigurationDictionary Configuration { get; set; } = new();

    public FlexFieldData()
    {
    }

    /// <summary>
    /// Copy constructor - takes a snapshot of any <see cref="IFlexFieldData"/>, including another
    /// <see cref="FlexFieldData"/>.
    /// </summary>
    public FlexFieldData(IFlexFieldData source)
    {
        Id = source.Id;
        Name = source.Name;
        DisplayName = source.DisplayName;
        Description = source.Description;
        FieldTypeName = source.FieldTypeName;
        Configuration = source.Configuration;
    }

    public FlexFieldData(
        Guid id,
        string name,
        string displayName,
        string fieldTypeName,
        string? description = null,
        FieldConfigurationDictionary? configuration = null)
    {
        Id = id;
        Name = name;
        DisplayName = displayName;
        FieldTypeName = fieldTypeName;
        Description = description;
        Configuration = configuration ?? new FieldConfigurationDictionary();
    }
}
