import { StyleBundle } from '@dignite/ng.flex-fields';

/**
 * CKEditor 5's UI stylesheet — loaded by the `CKEditor` field type's control component.
 *
 * The host's own copy: this is the same `ckeditor5` package the editor's JavaScript comes from at
 * runtime (`await import('ckeditor5')` in `CKEditorControlComponent.ngOnInit`), so the two halves
 * cannot drift apart the way a stylesheet compiled into this package would. See
 * `FlexFieldsStyleLoader` for the contract and this package's README for the `angular.json` entry.
 */
export const CKEDITOR5_STYLE: StyleBundle = {
  bundleName: 'ckeditor5',
  input: 'node_modules/ckeditor5/dist/ckeditor5.css',
};
