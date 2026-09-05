using System.Collections.Generic;

namespace Dignite.Abp.FlexFields.Matrix;

/// <summary>
/// One block type a <c>Matrix</c> field's configuration declares - a named, admin-authored schema
/// (<see cref="Fields"/>) that a block instance (<see cref="MatrixBlockValue"/>) can be an occurrence of.
/// Mirrors DynamicForms' own <c>MatrixBlockType</c>.
/// </summary>
public class MatrixBlockType
{
    /// <summary>
    /// Stable key a <see cref="MatrixBlockValue.BlockTypeName"/> refers back to. Not the display label -
    /// renaming a block type without updating already-stored blocks orphans them the same way renaming a
    /// top-level field does (see <c>FlexFieldDictionaryExtensions.RenameField</c>'s own remarks); nothing
    /// in this field type migrates that for you.
    /// </summary>
    public string Name { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public List<InlineFieldDefinition> Fields { get; set; } = new();
}
