using System;
using Dignite.Abp.FlexFields;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields.Demo.Entities;

/// <summary>
/// A flex field definition attached to <see cref="Product"/>: what field exists, which
/// <c>IFieldType</c> renders and validates it, and how that field type is configured.
/// </summary>
/// <remarks>
/// <para>
/// <b>Demo simplification, called out for anyone copying this:</b> <see cref="Required"/> and
/// <see cref="Searchable"/> live directly on this entity. The kernel deliberately keeps them off
/// <c>IFlexField</c> - see its XML doc - because the same field definition can be attached to several
/// host entity types with different Required/Searchable rules per host. This demo has exactly one host
/// type (<see cref="Product"/>), so folding the two flags into the definition is harmless. A real
/// downstream with more than one host type needs a separate per-usage attachment record instead - see
/// <c>flex-fields/test/Dignite.Abp.FlexFields.EntityFrameworkCore.Tests/TestArticleFieldUsage.cs</c> for
/// the two-entity shape (definition + usage) that scales to that case.
/// </para>
/// </remarks>
public class ProductField : AggregateRoot<Guid>, IFlexField
{
    /// <summary>
    /// Unique name of the field, and the key its value is stored under in
    /// <see cref="Product.FlexFields"/>. Renaming this is a data migration, not an edit - see
    /// <c>ProductFieldAppService.UpdateAsync</c>.
    /// </summary>
    public virtual string Name { get; set; } = string.Empty;

    public virtual string DisplayName { get; set; } = string.Empty;

    public virtual string? Description { get; set; }

    /// <summary>
    /// Registration key of the <c>IFieldType</c> this field is bound to - <c>"TextEdit"</c>,
    /// <c>"NumericEdit"</c>, <c>"DateEdit"</c>, <c>"Select"</c>, <c>"Switch"</c>, <c>"TreeView"</c> for
    /// the built-in types. This is a stored value, not a class name.
    /// </summary>
    public virtual string FieldTypeName { get; set; } = string.Empty;

    public virtual FieldConfigurationDictionary Configuration { get; set; }

    /// <summary>Whether a product must supply a value for this field. Enforced by <c>IFlexFieldValidator&lt;Product&gt;</c>.</summary>
    public virtual bool Required { get; set; }

    /// <summary>
    /// Whether this field's value is indexed for querying. Flipping this requires an
    /// <c>IFlexFieldIndexManager&lt;Product&gt;.RebuildAsync()</c> afterwards - see
    /// <c>ProductFieldAppService.UpdateAsync</c>.
    /// </summary>
    public virtual bool Searchable { get; set; }

    protected ProductField()
    {
        Configuration = new FieldConfigurationDictionary();
    }

    public ProductField(Guid id, string name, string displayName, string fieldTypeName)
        : base(id)
    {
        Name = name;
        DisplayName = displayName;
        FieldTypeName = fieldTypeName;
        Configuration = new FieldConfigurationDictionary();
    }
}
