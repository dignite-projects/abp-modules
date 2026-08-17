import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorMode } from './ckeditor-mode';
import { buildEditorConfig, resolveEditorClass } from './ckeditor-editor-config';

// Each plugin/editor export is its own name as a marker string - assertions can check exactly which
// ones landed in the composed config without ever loading the real, multi-megabyte ckeditor5 package.
const PLUGIN_NAMES = [
  'Essentials', 'Paragraph', 'Heading', 'Bold', 'Italic', 'Underline', 'Strikethrough', 'Link', 'List',
  'CodeBlock', 'BlockQuote', 'Table', 'TableToolbar', 'SourceEditing', 'Image', 'ImageUpload',
  'ImageToolbar', 'ImageCaption', 'ImageStyle', 'Markdown', 'ClassicEditor', 'BalloonEditor',
];

function fakeModule(): Parameters<typeof buildEditorConfig>[0] {
  const module: Record<string, string> = {};
  for (const name of PLUGIN_NAMES) {
    module[name] = name;
  }
  return module as unknown as Parameters<typeof buildEditorConfig>[0];
}

const BASE_PLUGINS = [
  'Essentials', 'Paragraph', 'Heading', 'Bold', 'Italic', 'Underline', 'Strikethrough', 'Link', 'List',
  'CodeBlock',
];
const BASE_TOOLBAR = [
  'heading', '|', 'bold', 'italic', 'underline', 'strikethrough', '|', 'link', 'bulletedList',
  'numberedList', '|', 'codeBlock',
];

describe('buildEditorConfig', () => {
  it('gives Basic mode only the plain text-adjustments toolbar', () => {
    const config = buildEditorConfig(fakeModule(), {
      mode: CKEditorMode.Basic,
      contentFormat: CKEditorContentFormat.Html,
      imageUploadEnabled: false,
    });

    expect(config.plugins).toEqual(BASE_PLUGINS);
    expect(config.toolbar).toEqual(BASE_TOOLBAR);
  });

  it('adds blockquote, table, undo/redo, and source editing for Full mode without image upload', () => {
    const config = buildEditorConfig(fakeModule(), {
      mode: CKEditorMode.Full,
      contentFormat: CKEditorContentFormat.Html,
      imageUploadEnabled: false,
    });

    expect(config.plugins).toEqual([...BASE_PLUGINS, 'BlockQuote', 'Table', 'TableToolbar', 'SourceEditing']);
    expect(config.toolbar).toEqual([
      ...BASE_TOOLBAR, '|', 'blockQuote', 'insertTable', '|', 'undo', 'redo', '|', 'sourceEditing',
    ]);
  });

  it('never adds image plugins in Basic mode, even when an images container is configured', () => {
    const config = buildEditorConfig(fakeModule(), {
      mode: CKEditorMode.Basic,
      contentFormat: CKEditorContentFormat.Html,
      imageUploadEnabled: true,
    });

    expect(config.plugins).not.toContain('Image');
    expect(config.toolbar).not.toContain('uploadImage');
  });

  it('inserts the upload button before undo/redo when image upload is enabled in Full mode', () => {
    const config = buildEditorConfig(fakeModule(), {
      mode: CKEditorMode.Full,
      contentFormat: CKEditorContentFormat.Html,
      imageUploadEnabled: true,
    });

    expect(config.plugins).toEqual([
      ...BASE_PLUGINS, 'BlockQuote', 'Table', 'TableToolbar', 'SourceEditing',
      'Image', 'ImageUpload', 'ImageToolbar', 'ImageCaption', 'ImageStyle',
    ]);
    expect(config.toolbar).toEqual([
      ...BASE_TOOLBAR, '|', 'blockQuote', 'insertTable', '|', 'uploadImage', '|', 'undo', 'redo', '|',
      'sourceEditing',
    ]);
  });

  it('adds the Markdown plugin for Markdown content format, in either mode', () => {
    const basic = buildEditorConfig(fakeModule(), {
      mode: CKEditorMode.Basic,
      contentFormat: CKEditorContentFormat.Markdown,
      imageUploadEnabled: false,
    });
    const full = buildEditorConfig(fakeModule(), {
      mode: CKEditorMode.Full,
      contentFormat: CKEditorContentFormat.Markdown,
      imageUploadEnabled: false,
    });

    expect(basic.plugins).toContain('Markdown');
    expect(full.plugins).toContain('Markdown');
  });

  it('excludes MediaEmbed and sets the required GPL license key', () => {
    const config = buildEditorConfig(fakeModule(), {
      mode: CKEditorMode.Full,
      contentFormat: CKEditorContentFormat.Html,
      imageUploadEnabled: true,
    });

    expect(config.plugins).not.toContain('MediaEmbed');
    expect(config.licenseKey).toBe('GPL');
    expect(config.table).toEqual({ contentToolbar: ['tableColumn', 'tableRow', 'mergeTableCells'] });
  });
});

describe('resolveEditorClass', () => {
  it('resolves Basic to BalloonEditor, not InlineEditor', () => {
    expect(resolveEditorClass(fakeModule(), CKEditorMode.Basic)).toBe('BalloonEditor');
  });

  it('resolves Full to ClassicEditor', () => {
    expect(resolveEditorClass(fakeModule(), CKEditorMode.Full)).toBe('ClassicEditor');
  });
});
