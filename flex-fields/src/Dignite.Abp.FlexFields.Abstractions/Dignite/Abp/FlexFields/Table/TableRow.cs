namespace Dignite.Abp.FlexFields.Table;

/// <summary>
/// One row - what a <c>Table</c> field's value is a list of. Unlike <c>MatrixBlockValue</c> there is no
/// type-tag: every row shares the same <see cref="TableConfiguration.Columns"/> schema, so there is
/// nothing to disambiguate. Reuses <see cref="FlexFieldDictionary"/> as-is for the cell values, same as
/// <c>MatrixBlockValue.Values</c>.
/// </summary>
public class TableRow
{
    public FlexFieldDictionary Values { get; set; } = new();
}
