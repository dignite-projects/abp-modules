import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FilePreviewComponent } from './file-preview.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FilePreviewComponent', () => {
  let component: FilePreviewComponent;
  let fixture: ComponentFixture<FilePreviewComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FilePreviewComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FilePreviewComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
