import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SimpleChange } from '@angular/core';

import { FileExplorerModalComponent } from './file-explorer-modal.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileExplorerModalComponent', () => {
  let component: FileExplorerModalComponent;
  let fixture: ComponentFixture<FileExplorerModalComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileExplorerModalComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileExplorerModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should mark a failed upload without throwing', async () => {
    const file = { name: 'failed.txt', size: 1 } as any;
    const testComponent = Object.create(FileExplorerModalComponent.prototype) as FileExplorerModalComponent;
    (testComponent as any).sizeLimit = 1024;
    (testComponent as any).list = { get: () => undefined };
    (testComponent as any).uploadingFiles = [file];
    (testComponent as any).uploadFile = () => Promise.reject(new Error('upload failed'));

    await testComponent.onFileInputChange({ target: { files: [file] } } as any);
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(file.status).toBe(2);
  });

  it('should initialize the selection with an empty array when the input is undefined', () => {
    component.ngOnChanges({
      selectPickerFile: new SimpleChange(undefined, undefined, true),
    });

    expect(component.selectedFiles).toEqual([]);
  });

  it('should preserve the in-progress selection when another input changes', () => {
    const selectedFile = { id: 'selected' } as any;
    component.selectedFiles = [selectedFile];

    component.ngOnChanges({
      multiple: new SimpleChange(false, true, false),
    });

    expect(component.selectedFiles).toEqual([selectedFile]);
  });

  it('should clone the selection when selectPickerFile changes', () => {
    const selectedFile = { id: 'selected' };
    const inputFiles = [selectedFile];

    component.ngOnChanges({
      selectPickerFile: new SimpleChange(undefined, inputFiles, true),
    });

    expect(component.selectedFiles).toEqual(inputFiles);
    expect(component.selectedFiles).not.toBe(inputFiles);
  });

  it('should add a checked file to the selection', () => {
    const selectedFile = { id: 'selected' } as any;

    component.onRowCheckboxChange({ target: { checked: true } } as any, selectedFile, [selectedFile]);

    expect(component.selectedFiles).toHaveLength(1);
    expect(component.selectedFiles[0]).toBe(selectedFile);
  });

  it('should use the file list sort icon convention', () => {
    expect(component.tableSortIcons).toEqual({
      sortAscending: 'datatable-icon-down',
      sortDescending: 'datatable-icon-up',
    });
  });
});
