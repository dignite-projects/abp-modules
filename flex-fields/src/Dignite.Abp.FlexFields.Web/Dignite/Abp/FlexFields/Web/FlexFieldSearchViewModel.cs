namespace Dignite.Abp.FlexFields.Web;

/// <summary>Model for a <c>Views/Shared/FlexFields/Search/{FieldTypeName}.cshtml</c> search partial.</summary>
public class FlexFieldSearchViewModel
{
    public FlexFieldValue Field { get; }

    /// <summary>
    /// Base HTML form control name the rendered input(s) should use - e.g. a range control renders
    /// <c>"{Name}Min"</c>/<c>"{Name}Max"</c> - so the host's controller/page can read whatever was
    /// submitted back by name and translate it into a <c>FlexFieldQueryCondition</c> itself. A search
    /// partial only ever renders inputs; it never builds a query condition, the same boundary the
    /// Angular library draws (see the FlexFields demo's <c>ProductsComponent</c>, which owns that
    /// translation because the library can't).
    /// </summary>
    public string Name { get; }

    public FlexFieldSearchViewModel(FlexFieldValue field, string name)
    {
        Field = field;
        Name = name;
    }
}
