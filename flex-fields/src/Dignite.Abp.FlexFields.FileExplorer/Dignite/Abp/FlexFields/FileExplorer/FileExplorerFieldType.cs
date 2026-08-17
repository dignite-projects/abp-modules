using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using Dignite.Abp.FlexFields.FileExplorer.Localization;

namespace Dignite.Abp.FlexFields.FileExplorer;

/// <summary>
/// The <c>FileExplorer</c> field type: one or more files picked through Dignite.FileExplorer's Angular
/// picker (<c>@dignite/ng.flex-fields-file-explorer</c>).
///
/// <para>
/// <b>Not indexable on purpose.</b> The value this field type stores is whatever the picker hands
/// back - an array of file descriptor objects (id, container, blob name, preview url, ...), not a bare
/// scalar or list of scalars every other indexable field type stores. <see cref="IFieldType.IndexValueType"/>
/// exists precisely for field types with nothing sensible to put in a typed index column - the same
/// reason a RichText or Matrix field type would return null here - and the Angular side ships no search
/// UI for this type for the identical reason. A future revision that narrows the stored value to bare
/// file ids could reasonably index those as <see cref="FlexFieldValueType.Guid"/> (multi-valued, the
/// same way <c>Select</c>/<c>Tree</c> index each selected option); that is a value-shape change on
/// both sides together, not something to bolt on unilaterally here.
/// </para>
///
/// <para>
/// <b>Zero reference to Dignite.FileExplorer.</b> Validating that a stored file id still exists, or
/// that a configured container is real, would need this project to depend on FileExplorer's domain -
/// exactly the cross-module reference the repository's own invariants forbid between file-storing and
/// flex-fields. So this type validates only what it can see: presence, per <c>Required</c>. Anything
/// deeper is the picker's job at pick time and FileExplorer's own authorization at fetch time.
/// </para>
/// </summary>
public class FileExplorerFieldType : FieldTypeBase
{
    public const string ControlName = "FileExplorer";

    public FileExplorerFieldType()
    {
        LocalizationResource = typeof(FlexFieldsFileExplorerResource);
    }

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:FileExplorer"];

    public override FlexFieldValueType? IndexValueType => null;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var errors = new List<ValidationResult>();

        if (args.Field.Required && !HasAnyValue(args.Field.Value))
        {
            errors.Add(
                new ValidationResult(
                    L["Validate:Required", args.Field.DisplayName],
                    new[] { args.Field.Name }
                    ));
        }

        return errors;
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new FileExplorerConfiguration(fieldConfiguration);
    }

    /// <summary>
    /// Storage-shape-agnostic presence check, in the spirit of <see cref="FieldTypeBase.ReadStringList"/>
    /// but for a value that is a list of objects rather than a list of strings: a fresh in-memory value is
    /// some <see cref="IEnumerable"/>, a value that has round-tripped through JSON is a <see cref="JsonElement"/>.
    /// </summary>
    private static bool HasAnyValue(object? value)
    {
        switch (value)
        {
            case null:
                return false;
            // Before IEnumerable: a string is a sequence of chars, and this field type never stores one,
            // but a stray string value should still read as "present" rather than "a sequence of chars".
            case string str:
                return !string.IsNullOrEmpty(str);
            case JsonElement element:
                return element.ValueKind switch
                {
                    JsonValueKind.Null or JsonValueKind.Undefined => false,
                    JsonValueKind.Array => element.GetArrayLength() > 0,
                    _ => true,
                };
            case IEnumerable enumerable:
                return enumerable.Cast<object?>().Any();
            default:
                return true;
        }
    }
}
