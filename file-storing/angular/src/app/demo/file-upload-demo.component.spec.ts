import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FileUploadDemoComponent } from './file-upload-demo.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileUploadDemoComponent', () => {
  let component: FileUploadDemoComponent;
  let fixture: ComponentFixture<FileUploadDemoComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileUploadDemoComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileUploadDemoComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
