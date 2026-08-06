/**
 * How much editing power a field's CKEditor 5 instance gets - `Basic` (`BalloonEditor`: floating
 * toolbar on selection, no persistent toolbar bar; text formatting and lists only) or `Full`
 * (`ClassicEditor`, persistent toolbar bar, every toolbar feature this bolt-on ships - blockquote,
 * table, image upload, undo/redo, source editing). Named for that difference in editing power, not for
 * the underlying CKEditor 5 editor class - see `buildEditorConfig`/`resolveEditorClass` in
 * `ckeditor-editor-config.ts` for the exact plugin/toolbar composition per mode and why `BalloonEditor`
 * rather than CKEditor 5's own `InlineEditor`. Mirrors the `CKEditorMode` enum on the server - the
 * ordinals are the stored value of `CKEditor.Mode`.
 */
export enum CKEditorMode {
  Basic = 0,
  Full = 1,
}
