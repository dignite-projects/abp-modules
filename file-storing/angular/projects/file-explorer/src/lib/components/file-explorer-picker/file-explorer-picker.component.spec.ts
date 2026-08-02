import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FileExplorerPickerComponent } from './file-explorer-picker.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileExplorerPickerComponent', () => {
  let component: FileExplorerPickerComponent;
  let fixture: ComponentFixture<FileExplorerPickerComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileExplorerPickerComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileExplorerPickerComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
