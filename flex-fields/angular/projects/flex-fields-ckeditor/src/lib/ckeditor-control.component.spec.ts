import { NgZone } from '@angular/core';
import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import type { Editor } from 'ckeditor5';
import { CKEditorControlComponent } from './ckeditor-control.component';
import { CKEditorUploadAdapter } from './ckeditor-upload-adapter';

// ngOnInit's own describe block mocks this - plain stand-ins for every plugin class
// buildEditorConfig/resolveEditorClass reference, none of which that test ever instantiates or
// inspects (both functions only ever push these into a plugins array or return one directly). Real
// ckeditor5 stays untouched for every other test in this file.
vi.mock('ckeditor5', () => {
  const plugin = {};
  return {
    ClassicEditor: plugin,
    BalloonEditor: plugin,
    Essentials: plugin,
    Paragraph: plugin,
    Heading: plugin,
    Bold: plugin,
    Italic: plugin,
    Underline: plugin,
    Strikethrough: plugin,
    Link: plugin,
    List: plugin,
    CodeBlock: plugin,
    BlockQuote: plugin,
    Table: plugin,
    TableToolbar: plugin,
    SourceEditing: plugin,
    Image: plugin,
    ImageUpload: plugin,
    ImageToolbar: plugin,
    ImageCaption: plugin,
    ImageStyle: plugin,
    Markdown: plugin,
  };
});

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'body',
      displayName: 'Body',
      fieldTypeName: 'CKEditor',
      configuration: { 'CKEditor.InitialContent': '<p>Default content</p>' },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

describe('CKEditorControlComponent', () => {
  beforeEach(() => {
    // @ngx-validate/core's validation directive attaches to any [formGroupName]/[formControlName]
    // element and needs its blueprints token even though TestBed.createComponent() never runs CD here
    // - view creation alone is enough to construct it.
    TestBed.configureTestingModule({
      providers: [{ provide: RestService, useValue: {} }],
      imports: [NgxValidateCoreModule.forRoot()],
    });
  });

  // createControl / onReady below never call fixture.detectChanges(): that would run ngOnInit(),
  // which is exactly what the dedicated ngOnInit describe block further down exists to exercise in
  // isolation. createControl() fires synchronously off the @Input setters (inherited from
  // FieldTypeControlBase), so its seeding logic is fully exercised without ever touching ngOnInit.
  function build(field: FlexFieldValue, selected?: unknown) {
    const values = new FormGroup({});
    const entity = new FormGroup({ flexFields: values });
    const fixture = TestBed.createComponent(CKEditorControlComponent);
    // Order matches FlexFieldControlComponent's own setInput sequence (fields, parentFieldName,
    // selected, entity) deliberately: rebuild() only does real work once entity/parentFieldName/fields
    // are all set, and entity is set last there. This component also tracks `hasStoredValue` off the
    // `selected` setter, which the base class's memoization guard does not compare - setting `entity`
    // before `selected` would let a same-value `selected` (e.g. '' after already defaulting to '')
    // silently skip rebuilding, masking a real hasStoredValue change.
    fixture.componentRef.setInput('fields', field);
    fixture.componentRef.setInput('parentFieldName', 'flexFields');
    if (selected !== undefined) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.componentRef.setInput('entity', entity);
    return { fixture, values };
  }

  describe('createControl', () => {
    it('seeds a brand-new field from the configured InitialContent', () => {
      const { values } = build(fieldValue());

      expect(values.get('body')!.value).toBe('<p>Default content</p>');
    });

    it('uses the stored value over InitialContent once a field has one', () => {
      const { values } = build(fieldValue(), '<p>Saved content</p>');

      expect(values.get('body')!.value).toBe('<p>Saved content</p>');
    });

    it('lets an explicitly-saved empty string win over InitialContent - the user really did clear it', () => {
      const { values } = build(fieldValue(), '');

      expect(values.get('body')!.value).toBe('');
    });

    it('falls back to InitialContent when the field has never had a stored value', () => {
      const { values } = build(fieldValue(), null);

      expect(values.get('body')!.value).toBe('<p>Default content</p>');
    });

    it('adds a required validator when the usage requires the field', () => {
      const { values } = build(fieldValue({ required: true }));

      expect(values.get('body')!.hasValidator(Validators.required)).toBe(true);
    });
  });

  describe('onReady', () => {
    function componentFor(configuration: Record<string, unknown>) {
      return build(fieldValue({ field: { ...fieldValue().field, configuration } })).fixture.componentInstance;
    }

    it('wires a file-explorer upload adapter when an images container is configured', () => {
      const component = componentFor({ 'CKEditor.ImagesContainerName': 'pics' });
      const fileRepository: { createUploadAdapter?: (loader: unknown) => unknown } = {};
      const editor = { plugins: { get: vi.fn(() => fileRepository) } } as unknown as Editor;

      component.onReady(editor);

      expect(editor.plugins.get).toHaveBeenCalledWith('FileRepository');
      expect(fileRepository.createUploadAdapter).toBeInstanceOf(Function);
      expect(fileRepository.createUploadAdapter!({})).toBeInstanceOf(CKEditorUploadAdapter);
    });

    it('does not touch FileRepository when no images container is configured', () => {
      const component = componentFor({});
      const editor = { plugins: { get: vi.fn() } } as unknown as Editor;

      component.onReady(editor);

      expect(editor.plugins.get).not.toHaveBeenCalled();
    });
  });

  describe('ngOnInit', () => {
    // Zone.js does not patch dynamic import() - the continuation after `await import('ckeditor5')`
    // resumes outside Angular's zone, so assigning editorClass/editorConfig there directly never
    // triggers change detection: the view stays on the template's Loading branch even though the
    // editor is already fully ready. This guards the fix (wrapping that assignment in ngZone.run())
    // by asserting on delegation to NgZone rather than on a real CKEditor mount, which needs no
    // stand-in beyond what ckeditor5 mock already provides at file scope.
    it('assigns editorClass/editorConfig inside NgZone.run so the async ckeditor5 import can trigger change detection once resolved', async () => {
      const { fixture } = build(fieldValue());
      // Spies on the real NgZone rather than replacing it: Angular's own zoneless change-detection
      // scheduler depends on other members of this service, which a bare { run: ... } stand-in
      // does not have. vi.spyOn preserves the real implementation, so the app's own scheduling stays
      // intact - only the call itself is observed.
      const ngZoneRunSpy = vi.spyOn(TestBed.inject(NgZone), 'run');

      fixture.detectChanges();

      await vi.waitFor(() => expect(ngZoneRunSpy).toHaveBeenCalled());

      expect(fixture.componentInstance.editorClass).not.toBeNull();
      expect(fixture.componentInstance.editorConfig).not.toBeNull();
    });
  });
});
