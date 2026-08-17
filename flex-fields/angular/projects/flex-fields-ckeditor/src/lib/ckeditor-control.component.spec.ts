import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { RestService } from '@abp/ng.core';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import type { Editor } from 'ckeditor5';
import { CKEditorControlComponent } from './ckeditor-control.component';
import { CKEditorUploadAdapter } from './ckeditor-upload-adapter';

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

  // Deliberately never calls fixture.detectChanges() anywhere in this file: that would run ngOnInit(),
  // which does a real `await import('ckeditor5')` against the actual multi-megabyte package.
  // createControl() itself fires synchronously off the @Input setters (inherited from
  // FieldTypeControlBase), so its seeding logic is fully exercised without ever touching ckeditor5.
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
});
