import type { EditorConfig } from 'ckeditor5';
import type { EditorRelaxedConstructor } from '@ckeditor/ckeditor5-integrations-common';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorMode } from './ckeditor-mode';

/**
 * Everything the `ckeditor5` package exports, as returned by `await import('ckeditor5')`. Typed this
 * way - never importing an individual plugin class as a static top-level symbol - so ckeditor5's
 * multi-megabyte payload stays inside the dynamic import's lazy chunk instead of the bundler pulling
 * it into the main bundle because some file imports it eagerly.
 */
type CKEditor5Module = typeof import('ckeditor5');

export interface BuildEditorConfigOptions {
  mode: CKEditorMode;
  contentFormat: CKEditorContentFormat;
  imageUploadEnabled: boolean;
}

/**
 * Composes this bolt-on's toolbar. Full mode gets every toolbar feature this bolt-on ships: headings,
 * basic styles, lists, links, code blocks, blockquote, tables, (when an images container is configured)
 * image upload, and a Source button. Basic is deliberately a lighter, inline-text-only experience - see
 * `resolveEditorClass`'s own doc on why it exists as a separate mode at all - so it drops every
 * block-structure/media/tooling feature that doesn't fit plain text adjustments: blockquote, table,
 * image upload, undo/redo, and source editing (which CKEditor 5's own SourceEditing plugin only
 * supports for ClassicEditor to begin with). Both modes deliberately exclude MediaEmbed (the legacy
 * oembed-to-iframe conversion is a sanitizer/security complication not worth taking on unrequested; a
 * possible follow-up, not a v1 gap to silently paper over).
 *
 * `licenseKey: 'GPL'` is required by CKEditor 5's own licensing for this self-hosted usage since v44 -
 * see https://ckeditor.com/legal/ckeditor-licensing-options. This bolt-on ships under GPL terms; a host
 * wanting the commercial terms swaps this by registering its own `FieldTypeDefinition` under the same
 * `'CKEditor'` name (see `FieldTypeDefinition`'s own doc on a later registration overriding an earlier
 * one), or forking this function.
 */
export function buildEditorConfig(module: CKEditor5Module, options: BuildEditorConfigOptions): EditorConfig {
  const isFull = options.mode === CKEditorMode.Full;

  const plugins: EditorConfig['plugins'] = [
    module.Essentials,
    module.Paragraph,
    module.Heading,
    module.Bold,
    module.Italic,
    module.Underline,
    module.Strikethrough,
    module.Link,
    module.List,
    module.CodeBlock,
  ];

  const toolbar: string[] = [
    'heading',
    '|',
    'bold',
    'italic',
    'underline',
    'strikethrough',
    '|',
    'link',
    'bulletedList',
    'numberedList',
    '|',
    'codeBlock',
  ];

  if (isFull) {
    // Blockquote, tables, undo/redo, and source editing are all Full-only - Basic stays a plain
    // text-adjustments toolbar (heading/basic styles/lists/code block only, from the shared list above).
    plugins.push(module.BlockQuote, module.Table, module.TableToolbar, module.SourceEditing);
    toolbar.push('|', 'blockQuote', 'insertTable', '|', 'undo', 'redo', '|', 'sourceEditing');

    if (options.imageUploadEnabled) {
      // Image, ImageUpload, ImageToolbar are the minimum for a working upload button; ImageCaption and
      // ImageStyle give ImageToolbar's balloon something to show (align-left/center/right, a caption
      // toggle) rather than appearing near-empty. Verify this sub-list against the installed ckeditor5
      // version's docs at implementation time - it is the one part of this file most likely to have
      // grown new recommended companions since. Full-only like the rest of this block - Basic mode
      // never shows an upload button even if a stale ImagesContainerName is still on record (the config
      // designer clears it on switching to Basic, but this is the runtime-side guarantee).
      plugins.push(module.Image, module.ImageUpload, module.ImageToolbar, module.ImageCaption, module.ImageStyle);
      toolbar.splice(toolbar.indexOf('undo'), 0, 'uploadImage', '|');
    }
  }

  if (options.contentFormat === CKEditorContentFormat.Markdown) {
    // Swaps the editor's data processor from HTML to GFM - an instance-level choice, which is exactly
    // why this whole function runs once at creation time (CKEditorControlComponent.ngOnInit) rather
    // than being reactive to a later config change. Applies in both modes - Basic's reduced toolbar is
    // about which buttons show, not which data format the field stores.
    plugins.push(module.Markdown);
  }

  return {
    licenseKey: 'GPL',
    plugins,
    toolbar,
    table: {
      contentToolbar: ['tableColumn', 'tableRow', 'mergeTableCells'],
    },
  };
}

/**
 * Typed as `EditorRelaxedConstructor` (from `@ckeditor/ckeditor5-integrations-common`, the same type
 * `CKEditorComponent`'s own `[editor]` input expects) rather than a concrete editor class: CKEditor 5's
 * own editor classes are not structurally interchangeable under `typeof ClassicEditor` /
 * `typeof BalloonEditor` (each carries a distinct `editorName` string literal type), so a function that
 * can return either has to widen to the same "just needs a static `create()`" shape the component
 * itself uses.
 *
 * `Basic` resolves to `BalloonEditor`, not `InlineEditor`: both are toolbar-less-by-default, floating-
 * balloon-on-selection editors, but InlineEditor renders the editable region with no box/border at all
 * (indistinguishable from static page text until you click in), which read as broken/unfinished in this
 * admin UI. BalloonEditor keeps the same floating-toolbar interaction while giving the editable region
 * a visible boundary, like Full's editable area minus the persistent toolbar bar above it.
 */
export function resolveEditorClass(module: CKEditor5Module, mode: CKEditorMode): EditorRelaxedConstructor {
  return mode === CKEditorMode.Basic ? module.BalloonEditor : module.ClassicEditor;
}
