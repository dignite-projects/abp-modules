namespace Dignite.Abp.FlexFields.Matrix;

/// <summary>
/// One block instance - what a <c>Matrix</c> field's value is a list of. Records which
/// <see cref="MatrixBlockType"/> it is an occurrence of, and that block type's own field values, keyed by
/// <see cref="InlineFieldDefinition.Name"/>. Reuses <see cref="FlexFieldDictionary"/> as-is for the bag
/// rather than inventing a parallel type - it is already exactly "a dictionary of flex field values",
/// which is exactly what a block instance needs.
/// </summary>
public class MatrixBlockValue
{
    public string BlockTypeName { get; set; } = default!;

    public FlexFieldDictionary Values { get; set; } = new();
}
