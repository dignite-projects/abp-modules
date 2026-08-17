import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '@dignite/ng.flex-fields';
import { FileExplorerConfigComponent } from './file-explorer-config.component';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'attachments',
    displayName: 'Attachments',
    fieldTypeName: 'FileExplorer',
    configuration: {},
    ...overrides,
  };
}

describe('FileExplorerConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  function render(selected?: FlexFieldData) {
    const entity = new FormGroup({});
    const fixture = TestBed.createComponent(FileExplorerConfigComponent);
    fixture.componentRef.setInput('type', 'FileExplorer');
    fixture.componentRef.setInput('Entity', entity);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, entity };
  }

  it('seeds the configuration group with the server defaults for a new field', () => {
    const { entity } = render();

    expect(entity.get(['configuration', 'FileExplorer.FileContainerName'])!.value).toBe('');
    expect(entity.get(['configuration', 'FileExplorer.UploadFileMultiple'])!.value).toBe(false);
  });

  it('requires a container name - there is no fallback container downstream to guess at', () => {
    const { entity } = render();

    expect(entity.get(['configuration', 'FileExplorer.FileContainerName'])!.valid).toBe(false);

    entity.get(['configuration', 'FileExplorer.FileContainerName'])!.setValue('attachments');

    expect(entity.get(['configuration', 'FileExplorer.FileContainerName'])!.valid).toBe(true);
  });

  it('patches in the stored configuration when editing a field of this type', () => {
    const { entity } = render(
      fieldData({
        configuration: { 'FileExplorer.FileContainerName': 'attachments', 'FileExplorer.UploadFileMultiple': true },
      }),
    );

    expect(entity.get(['configuration', 'FileExplorer.FileContainerName'])!.value).toBe('attachments');
    expect(entity.get(['configuration', 'FileExplorer.UploadFileMultiple'])!.value).toBe(true);
  });

  it('does not leak configuration from a field of a different type', () => {
    const { entity } = render(
      fieldData({ fieldTypeName: 'Text', configuration: { 'FileExplorer.FileContainerName': 'attachments' } }),
    );

    expect(entity.get(['configuration', 'FileExplorer.FileContainerName'])!.value).toBe('');
  });
});
