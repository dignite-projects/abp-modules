using System;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Plays the role of a downstream host's own field-definition entity (e.g. a CMS's <c>Field</c>): its own
/// aggregate, which also implements the kernel's <see cref="IFlexField"/> contract.
/// </summary>
public class TestField : AggregateRoot<Guid>, IFlexField
{
    public virtual string Name { get; set; } = string.Empty;

    public virtual string DisplayName { get; set; } = string.Empty;

    public virtual string? Description { get; set; }

    public virtual string FieldTypeName { get; set; } = string.Empty;

    public virtual FieldConfigurationDictionary Configuration { get; set; }

    protected TestField()
    {
        Configuration = new FieldConfigurationDictionary();
    }

    public TestField(Guid id, string name, string fieldTypeName, string? description = null)
        : base(id)
    {
        Name = name;
        DisplayName = name;
        FieldTypeName = fieldTypeName;
        Description = description;
        Configuration = new FieldConfigurationDictionary();
    }
}
