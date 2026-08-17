import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import { FileExplorerControlComponent } from './file-explorer-control.component';

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'attachments',
      displayName: 'Attachments',
      fieldTypeName: 'FileExplorer',
      configuration: { 'FileExplorer.FileContainerName': 'attachments' },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(FileExplorerControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.componentRef.setInput('entity', entity);
  fixture.detectChanges();
  return { fixture, values, component: fixture.componentInstance };
}

describe('FileExplorerControlComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  describe('configuration getters', () => {
    it('reads multiple and the container name off the configuration', () => {
      const { component } = render(
        fieldValue({
          field: {
            id: '1', name: 'attachments', displayName: 'Attachments', fieldTypeName: 'FileExplorer',
            configuration: { 'FileExplorer.FileContainerName': 'attachments', 'FileExplorer.UploadFileMultiple': true },
          },
        }),
      );

      expect(component.multiple).toBe(true);
      expect(component.containerName).toBe('attachments');
      expect(component.isContainerConfigured).toBe(true);
    });

    it('flags a field with no configured container as unconfigured, not defaulted', () => {
      const { component } = render(
        fieldValue({
          field: {
            id: '1', name: 'attachments', displayName: 'Attachments', fieldTypeName: 'FileExplorer',
            configuration: {},
          },
        }),
      );

      expect(component.containerName).toBe('');
      expect(component.isContainerConfigured).toBe(false);
    });
  });

  describe('createControl', () => {
    it('starts with an empty file list when nothing is stored', () => {
      const { values } = render(fieldValue());

      expect(values.get('attachments')!.value).toEqual([]);
    });

    it('uses the stored file list', () => {
      // size is required here: the real fe-file-explorer-picker this renders into formats it via
      // FormatFileSizePipe, which throws on undefined.
      const stored = [{ url: 'a.png', name: 'a.png', size: 1024 }];

      const { values } = render(fieldValue(), stored);

      expect(values.get('attachments')!.value).toEqual(stored);
    });

    it('falls back to an empty list rather than a non-array stored value', () => {
      const { values } = render(fieldValue(), 'not-an-array');

      expect(values.get('attachments')!.value).toEqual([]);
    });

    it('adds a required validator when the usage requires the field', () => {
      const { values } = render(fieldValue({ required: true }));

      expect(values.get('attachments')!.hasValidator(Validators.required)).toBe(true);
    });
  });

  describe('onSelectedFileChange', () => {
    it('writes the newly picked files into the field control', () => {
      const { component, values } = render(fieldValue(), []);
      const picked = [{ url: 'a.png', name: 'a.png' }];

      component.onSelectedFileChange(picked);

      expect(values.get('attachments')!.value).toEqual(picked);
    });
  });

  describe('rendering', () => {
    it('shows a warning instead of the picker when no container is configured', () => {
      const { fixture } = render(
        fieldValue({
          field: {
            id: '1', name: 'attachments', displayName: 'Attachments', fieldTypeName: 'FileExplorer',
            configuration: {},
          },
        }),
      );

      expect(fixture.nativeElement.querySelector('fe-file-explorer-picker')).toBeFalsy();
      expect(fixture.nativeElement.querySelector('.alert-warning')).toBeTruthy();
    });

    it('renders the file-explorer picker once a container is configured', () => {
      const { fixture } = render(fieldValue());

      expect(fixture.nativeElement.querySelector('fe-file-explorer-picker')).toBeTruthy();
      expect(fixture.nativeElement.querySelector('.alert-warning')).toBeFalsy();
    });
  });
});
