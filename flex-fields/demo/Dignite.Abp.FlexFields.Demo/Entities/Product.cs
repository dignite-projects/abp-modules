using System;
using Dignite.Abp.FlexFields;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.FlexFields.Demo.Entities;

/// <summary>
/// The demo's host entity: an ordinary aggregate that also carries a flex field value bag. Plays the
/// role a real downstream's own entity would (e.g. a CMS's <c>Entry</c>).
/// </summary>
public class Product : AggregateRoot<Guid>, IHasFlexFields
{
    public virtual string Name { get; set; } = string.Empty;

    /// <summary>
    /// The value bag every flex field's value is read from and written to, keyed by
    /// <see cref="ProductField.Name"/>. Must be initialized in every constructor - reading or writing
    /// through <see cref="FlexFieldDictionaryExtensions"/> on a null bag throws.
    /// </summary>
    public virtual FlexFieldDictionary FlexFields { get; set; }

    protected Product()
    {
        FlexFields = new FlexFieldDictionary();
    }

    public Product(Guid id, string name)
        : base(id)
    {
        Name = name;
        FlexFields = new FlexFieldDictionary();
    }
}
