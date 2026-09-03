import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
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

// Stands in for the real `@ckeditor/ckeditor5-angular` package - `CKEditorControlComponent`'s own
// `imports: [..., CKEditorModule]` (unmodified, no TestBed.overrideComponent needed) picks up this
// mock directly, since it imports `CKEditorModule` from this same module specifier. See
// ckeditor-angular-mock.ts for what it stands in for and why it lives in its own file.
vi.mock('@ckeditor/ckeditor5-angular', () => import('./ckeditor-angular-mock'));

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

/**
 * Reproduces the real consumer shape (`DocumentDetailComponent`, out of this repo): an `OnPush` host
 * rendering `ff-ckeditor-control` directly, inputs set once before the first `detectChanges()`. This is
 * what the `ngOnInit` describe block below exercises - see the comment there for why a plain-field
 * assignment inside the component under an `OnPush` ancestor like this one is invisible to change
 * detection, and a signal write is not.
 */
@Component({
  selector: 'ff-onpush-host',
  template: `<ff-ckeditor-control [fields]="fields" [entity]="entity" parentFieldName="flexFields" />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CKEditorControlComponent],
})
class OnPushHostComponent {
  @Input() fields!: FlexFieldValue;
  @Input() entity!: FormGroup;
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
    // Reproduces the real consumer: this component is always created dynamically
    // (`ViewContainerRef.createComponent`, see `FlexFieldControlComponent`) inside whatever host
    // renders it, and that host is `OnPush` in the real consumer (`DocumentDetailComponent`). A tick
    // that reaches an `OnPush` ancestor nothing has marked dirty skips its subtree entirely, so a plain
    // field assignment made after `await import('ckeditor5')` resolves (outside Angular's zone) would
    // never repaint here - the earlier `ngZone.run()` fix only addressed being outside the zone, not
    // being under an unmarked `OnPush` ancestor, and it stayed stuck on the template's Loading branch.
    function buildOnPushHost(field: FlexFieldValue) {
      const values = new FormGroup({});
      const entity = new FormGroup({ flexFields: values });
      const fixture = TestBed.createComponent(OnPushHostComponent);
      fixture.componentRef.setInput('fields', field);
      fixture.componentRef.setInput('entity', entity);
      return fixture;
    }

    it('renders the editor once the lazy ckeditor5 import resolves, with no further input change or DOM event on the OnPush host', async () => {
      const fixture = buildOnPushHost(fieldValue());

      fixture.detectChanges();

      expect(fixture.nativeElement.textContent).toContain('Loading');
      expect(fixture.nativeElement.querySelector('ckeditor')).toBeNull();

      // Polls `detectChanges()` rather than a single call after `whenStable()`: dynamic `import()`'s
      // continuation is not reliably tracked by the zone's pending-task bookkeeping `whenStable()`
      // relies on, so a single post-`whenStable()` check can race the still-pending import. Repeated
      // `detectChanges()` calls stand in for the app's own repeated ticks over time - never an input
      // change or a DOM event on the host - and the mutation check below confirms this genuinely
      // distinguishes the fix from the bug rather than papering over it: OnPush means an ancestor tick
      // that nothing marked dirty skips this subtree regardless of how many times it runs.
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(fixture.nativeElement.querySelector('ckeditor')).not.toBeNull();
      });

      expect(fixture.nativeElement.textContent).not.toContain('Loading');
    });

    // The Mode -> editor class / ContentFormat -> plugin mapping itself is already covered by
    // ckeditor-editor-config.spec.ts; nothing cheap to add here beyond re-asserting DOM presence, which
    // the test above already does.
  });
});
