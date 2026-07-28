namespace Dignite.Abp.FlexFields.MongoDB;

/// <summary>
/// Stands in for a downstream's per-usage attachment record (a CMS's <c>EntryField</c>): the field
/// definition plus the Required/Searchable flags that belong to this particular usage rather than to the
/// definition itself. Mutable so tests can flip <see cref="Searchable"/> mid-test.
/// </summary>
public class TestArticleFieldUsage
{
    public TestField Field { get; }

    public bool Searchable { get; set; }

    public bool Required { get; set; }

    public TestArticleFieldUsage(TestField field, bool searchable, bool required)
    {
        Field = field;
        Searchable = searchable;
        Required = required;
    }
}
