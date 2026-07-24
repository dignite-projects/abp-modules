import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SimpleChange } from '@angular/core';

import { FileModalComponent } from './file-modal.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileModalComponent', () => {
  let component: FileModalComponent;
  let fixture: ComponentFixture<FileModalComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileModalComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should mark a failed upload without throwing', async () => {
    const file = { name: 'failed.txt', size: 1 } as any;
    const testComponent = Object.create(FileModalComponent.prototype) as FileModalComponent;
    (testComponent as any).sizeLimit = 1024;
    (testComponent as any).list = { get: () => undefined };
    (testComponent as any).uploadPictureStatusList = [file];
    (testComponent as any).uploadingFile = () => Promise.reject(new Error('upload failed'));

    await testComponent.getFileChange({ target: { files: [file] } });
    await new Promise(resolve => setTimeout(resolve, 0));

    expect(file.status).toBe(2);
  });

  it('should initialize the selection with an empty array when the input is undefined', () => {
    component.ngOnChanges({
      selectPickerFile: new SimpleChange(undefined, undefined, true),
    });

    expect(component.selectedTable).toEqual([]);
  });

  it('should preserve the in-progress selection when another input changes', () => {
    const selectedFile = { id: 'selected' };
    component.selectedTable = [selectedFile];

    component.ngOnChanges({
      multiple: new SimpleChange(false, true, false),
    });

    expect(component.selectedTable).toEqual([selectedFile]);
  });

  it('should clone the selection when selectPickerFile changes', () => {
    const selectedFile = { id: 'selected' };
    const inputFiles = [selectedFile];

    component.ngOnChanges({
      selectPickerFile: new SimpleChange(undefined, inputFiles, true),
    });

    expect(component.selectedTable).toEqual(inputFiles);
    expect(component.selectedTable).not.toBe(inputFiles);
  });
});
