using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Dignite.Abp.FlexFields.CKEditor.Localization;

namespace Dignite.Abp.FlexFields.CKEditor;

/// <summary>
/// The <c>CKEditor</c> field type: rich text edited with CKEditor 5 (https://ckeditor.com), persisted
/// as a plain string - HTML by default, or GitHub Flavored Markdown when
/// <see cref="CKEditorConfiguration.ContentFormat"/> is <see cref="CKEditorContentFormat.Markdown"/>.
///
/// <para>
/// <b>Not indexable on purpose</b>, the same reasoning as <c>FileExplorerFieldType</c>:
/// <see cref="IFieldType.IndexValueType"/>'s own doc names RichText as the canonical null example, and
/// free-text rich content has no sensible typed index column to land in. No override of
/// <see cref="FieldTypeBase.GetSearchableValues"/> is needed - the base implementation already yields
/// nothing once <see cref="IndexValueType"/> is null.
/// </para>
///
/// <para>
/// <b>Zero reference to any rendering, sanitization or Markdown library.</b> This project stores and
/// validates a plain string only; converting Markdown to HTML and sanitizing it for display is
/// <c>Dignite.Abp.FlexFields.CKEditor.Web</c>'s job server-side, or the Angular control/view
/// components' job client-side - the same boundary <c>FileExplorerFieldType</c> keeps against
/// <c>Dignite.FileExplorer</c>.
/// </para>
/// </summary>
public class CKEditorFieldType : FieldTypeBase
{
    public const string ControlName = "CKEditor";

    public CKEditorFieldType()
    {
        LocalizationResource = typeof(FlexFieldsCKEditorResource);
    }

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:CKEditor"];

    public override FlexFieldValueType? IndexValueType => null;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var errors = new List<ValidationResult>();

        // Simple presence check, same spirit as TextFieldType's: a rich-text field's "is this really
        // empty" question (an editor can save "<p></p>" and mean nothing) is a UI-level concern, not
        // something the stored string alone can answer perfectly - not attempted here.
        if (args.Field.Required && string.IsNullOrWhiteSpace(args.Field.Value?.ToString()))
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
        return new CKEditorConfiguration(fieldConfiguration);
    }
}
