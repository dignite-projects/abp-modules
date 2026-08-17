import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '@dignite/ng.flex-fields';
import { CKEditorConfigComponent } from './ckeditor-config.component';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorMode } from './ckeditor-mode';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'body',
    displayName: 'Body',
    fieldTypeName: 'CKEditor',
    configuration: {},
    ...overrides,
  };
}

describe('CKEditorConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  function render(selected?: FlexFieldData) {
    const entity = new FormGroup({});
    const fixture = TestBed.createComponent(CKEditorConfigComponent);
    fixture.componentRef.setInput('type', 'CKEditor');
    fixture.componentRef.setInput('Entity', entity);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, entity, component: fixture.componentInstance };
  }

  it('seeds the configuration group with the server defaults for a new field', () => {
    const { entity } = render();

    expect(entity.get('configuration')!.value).toEqual({
      'CKEditor.Mode': CKEditorMode.Full,
      'CKEditor.ContentFormat': CKEditorContentFormat.Html,
      'CKEditor.ImagesContainerName': '',
      'CKEditor.InitialContent': '',
    });
  });

  it('patches in the stored configuration when editing a field of this type', () => {
    const { entity } = render(
      fieldData({
        configuration: { 'CKEditor.Mode': CKEditorMode.Basic, 'CKEditor.InitialContent': 'Hello' },
      }),
    );

    expect(entity.get(['configuration', 'CKEditor.Mode'])!.value).toBe(CKEditorMode.Basic);
    expect(entity.get(['configuration', 'CKEditor.InitialContent'])!.value).toBe('Hello');
  });

  it('does not leak configuration from a field of a different type', () => {
    const { entity } = render(
      fieldData({ fieldTypeName: 'Text', configuration: { 'CKEditor.Mode': CKEditorMode.Basic } }),
    );

    expect(entity.get(['configuration', 'CKEditor.Mode'])!.value).toBe(CKEditorMode.Full);
  });

  it('shows the images container field for a Full-mode field loaded from storage', () => {
    const { component } = render(fieldData({ configuration: { 'CKEditor.Mode': CKEditorMode.Full } }));

    expect(component['showImagesContainerName']).toBe(true);
  });

  it('hides the images container field for a Basic-mode field loaded from storage', () => {
    const { component } = render(fieldData({ configuration: { 'CKEditor.Mode': CKEditorMode.Basic } }));

    expect(component['showImagesContainerName']).toBe(false);
  });

  it('clears a stored images container name when the user switches to Basic mode', () => {
    const { component, entity } = render(
      fieldData({
        configuration: { 'CKEditor.Mode': CKEditorMode.Full, 'CKEditor.ImagesContainerName': 'pics' },
      }),
    );
    expect(entity.get(['configuration', 'CKEditor.ImagesContainerName'])!.value).toBe('pics');

    entity.get(['configuration', 'CKEditor.Mode'])!.setValue(CKEditorMode.Basic);
    component['onModeChange']();

    expect(component['showImagesContainerName']).toBe(false);
    expect(entity.get(['configuration', 'CKEditor.ImagesContainerName'])!.value).toBe('');
  });

  it('does not touch the images container name when switching to Full mode', () => {
    const { component, entity } = render(fieldData({ configuration: { 'CKEditor.Mode': CKEditorMode.Basic } }));

    entity.get(['configuration', 'CKEditor.Mode'])!.setValue(CKEditorMode.Full);
    component['onModeChange']();

    expect(component['showImagesContainerName']).toBe(true);
    expect(entity.get(['configuration', 'CKEditor.ImagesContainerName'])!.value).toBe('');
  });

  it('never mutates the stored configuration just from opening and cancelling out of an existing field', () => {
    // onConfigurationPatched (the initial-load path) re-syncs visibility but must not itself write
    // anything back - only a real onModeChange() (the user actually touching the Mode control) does.
    const selected = fieldData({
      configuration: { 'CKEditor.Mode': CKEditorMode.Basic, 'CKEditor.ImagesContainerName': 'stale' },
    });
    const { entity } = render(selected);

    expect(entity.get(['configuration', 'CKEditor.ImagesContainerName'])!.value).toBe('stale');
  });
});
