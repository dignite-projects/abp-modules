import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FilePickerComponent } from './file-picker.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FilePickerComponent', () => {
  let component: FilePickerComponent;
  let fixture: ComponentFixture<FilePickerComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FilePickerComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FilePickerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
