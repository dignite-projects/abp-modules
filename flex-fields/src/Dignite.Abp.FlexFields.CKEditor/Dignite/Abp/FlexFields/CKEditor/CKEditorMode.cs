namespace Dignite.Abp.FlexFields.CKEditor;

/// <summary>
/// How much editing power a field's CKEditor 5 instance gets - Basic (BalloonEditor: floating toolbar on
/// selection, no persistent toolbar bar; text formatting and lists only) or Full (ClassicEditor,
/// persistent toolbar bar, every toolbar feature this bolt-on ships - blockquote, table, image upload,
/// undo/redo, source editing). Named for that difference in editing power, not for the underlying
/// CKEditor 5 editor class: the class is an implementation detail downstream of this choice, not what a
/// field owner is actually choosing between. See the Angular library's ckeditor-editor-config.ts for the
/// exact plugin/toolbar composition per mode. Mirrored by the Angular library's CKEditorMode enum - the
/// ordinals are the stored value of CKEditorConfigurationNames.Mode (see
/// FieldConfigurationDictionaryExtensions.GetConfiguration, which reads an enum config value as its
/// numeric ordinal, not its name).
/// </summary>
public enum CKEditorMode
{
    Basic,
    Full
}
