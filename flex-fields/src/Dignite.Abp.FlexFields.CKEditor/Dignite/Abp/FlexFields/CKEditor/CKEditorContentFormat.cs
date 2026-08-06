namespace Dignite.Abp.FlexFields.CKEditor;

/// <summary>
/// Output format a CKEditor field's value is persisted in. Mirrored by the Angular library's
/// CKEditorContentFormat enum - the ordinals are the stored value of
/// CKEditorConfigurationNames.ContentFormat.
///
/// <para>
/// Markdown swaps CKEditor 5's data processor (the <c>Markdown</c> plugin) at editor-creation time -
/// an instance-level choice, not a runtime toggle. Changing this on a field that already has real
/// values does not retroactively convert them; existing values keep the old format until re-saved. No
/// value migration is provided for this - the same class of decision <c>IFlexFieldValueMigrator</c>
/// documents for field renames, left to a downstream if it ever needs one.
/// </para>
/// </summary>
public enum CKEditorContentFormat
{
    Html,
    Markdown
}
