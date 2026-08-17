import { TestBed } from '@angular/core/testing';
import { FieldTypeResolver } from '@dignite/ng.flex-fields';
import { CKEDITOR_FIELD_TYPE } from './ckeditor-field-type';
import { CKEditorConfigComponent } from './ckeditor-config.component';
import { CKEditorControlComponent } from './ckeditor-control.component';
import { CKEditorViewComponent } from './ckeditor-view.component';
import { provideCKEditorFieldType } from './provide-ckeditor-field-type';

describe('CKEDITOR_FIELD_TYPE', () => {
  it('registers under the stable "CKEditor" key the server expects', () => {
    // A stored value, not a naming choice - renaming it orphans every field already bound to it.
    expect(CKEDITOR_FIELD_TYPE.name).toBe('CKEditor');
  });

  it('wires config/control/view, and deliberately ships no search component', () => {
    expect(CKEDITOR_FIELD_TYPE.configComponent).toBe(CKEditorConfigComponent);
    expect(CKEDITOR_FIELD_TYPE.controlComponent).toBe(CKEditorControlComponent);
    expect(CKEDITOR_FIELD_TYPE.viewComponent).toBe(CKEditorViewComponent);
    expect(CKEDITOR_FIELD_TYPE.searchComponent).toBeUndefined();
  });
});

describe('provideCKEditorFieldType', () => {
  it('registers the CKEditor field type through the standard FLEX_FIELD_TYPES multi-provider', () => {
    TestBed.configureTestingModule({ providers: [provideCKEditorFieldType()] });

    const resolver = TestBed.inject(FieldTypeResolver);

    expect(resolver.get('CKEditor')).toBe(CKEDITOR_FIELD_TYPE);
  });
});
