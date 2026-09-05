using System;
using System.Collections.Generic;
using System.Linq;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// How deep a field definition is allowed to nest composite field types inside one another, and the
/// measurement that enforces it.
///
/// <para>
/// <b>Why a limit exists at all.</b> A composite type's configuration embeds whole field definitions
/// (<see cref="InlineFieldDefinition"/>), and those can name a composite type in turn - so a field
/// definition is a tree of unbounded depth. Nothing that walks it has a depth guard of its own:
/// <c>InlineFieldValidator</c> recurses through <see cref="IFieldType.Validate"/>,
/// <c>Dignite.Abp.FlexFields.Web</c>'s <c>Matrix.cshtml</c>/<c>Table.cshtml</c> recurse through the
/// <c>flex-field-view</c> dispatch, and the Angular designer and value editor recurse through
/// <c>ff-flex-field-config</c>/<c>ff-flex-field-control</c>. None of them can loop forever - the
/// configuration is embedded data, not a reference, so it cannot cycle - but all of them cost
/// depth-proportional stack and render work, and none of them is usable at depth. Capping the tree once,
/// where it is written, is what keeps every one of those readers safe without a guard apiece.
/// </para>
///
/// <para>
/// <b>The kernel measures; the host enforces.</b> There is no definition-save path in the kernel to hang
/// this off - a field definition is a downstream's own entity - so this is a pure function a host calls
/// before it accepts a configuration. The demo's <c>ProductFieldAppService</c> is the worked example.
/// </para>
/// </summary>
public static class CompositeFieldNesting
{
    /// <summary>
    /// How many levels of field definition a stored field may span, counting the field itself as 1.
    ///
    /// <para>
    /// At the current value of <c>3</c>: a top-level field may be composite, and so may its
    /// columns/sub-fields - one layer of nesting, e.g. a Table column that is itself a Matrix. What
    /// that nested composite in turn declares (level 3) must be <i>non-composite</i>: <c>Table &gt; Matrix &gt;
    /// Table</c> is level 4 and is refused.
    /// </para>
    ///
    /// <para>
    /// Mirrored client-side by <c>MAX_COMPOSITE_NESTING_DEPTH</c> in
    /// <c>angular/projects/flex-fields/src/lib/field-types/composite-nesting.ts</c>, which greys the
    /// choice out before it is made - the same "mirror the server's rule so the UI can pre-empt it"
    /// arrangement the field-name length limits use. This constant is the authority; that one is a
    /// courtesy.
    /// </para>
    /// </summary>
    public const int MaxDepth = 3;

    /// <summary>
    /// Whether a field definition nests composite types deeper than <see cref="MaxDepth"/> allows.
    /// </summary>
    public static bool ExceedsMaxDepth(
        string fieldTypeName,
        FieldConfigurationDictionary? configuration,
        IReadOnlyList<IFieldType> fieldTypes)
    {
        return MeasureDepth(fieldTypeName, configuration, fieldTypes, MaxDepth + 1) > MaxDepth;
    }

    /// <summary>
    /// Depth of the deepest branch of a field definition, counting the field itself as 1 - so a scalar
    /// field is 1, and a Table whose columns are all scalar is 2.
    ///
    /// <para>
    /// <paramref name="remaining"/> bounds the recursion itself. Without it this method would be the
    /// very stack overflow it exists to prevent: it is the first thing to walk a configuration that has
    /// arrived from a client and has not yet been vetted. The result is therefore accurate only up to
    /// the budget, which is all <see cref="ExceedsMaxDepth"/> needs to decide.
    /// </para>
    /// </summary>
    private static int MeasureDepth(
        string fieldTypeName,
        FieldConfigurationDictionary? configuration,
        IReadOnlyList<IFieldType> fieldTypes,
        int remaining)
    {
        if (remaining <= 1 || configuration == null)
        {
            return 1;
        }

        // An unregistered type name is not this method's error to raise - it is reported per-field by
        // InlineFieldValidator when a value is validated. Nothing can be nested under it either way.
        var fieldType = fieldTypes.FirstOrDefault(ft => ft.Name == fieldTypeName);
        if (fieldType is not ICompositeFieldType composite)
        {
            return 1;
        }

        var deepestInlineField = 0;
        foreach (var inlineField in composite.GetInlineFields(configuration))
        {
            deepestInlineField = Math.Max(
                deepestInlineField,
                MeasureDepth(inlineField.FieldTypeName, inlineField.Configuration, fieldTypes, remaining - 1));
        }

        // A composite that declares nothing yet is still just one level - an empty Table is not deeper
        // than a Text field, and refusing it would block the admin's very first save.
        return 1 + deepestInlineField;
    }
}
