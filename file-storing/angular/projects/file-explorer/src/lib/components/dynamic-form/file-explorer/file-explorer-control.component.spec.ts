import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';

import { FileExplorerControlComponent } from './file-explorer-control.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileExplorerControlComponent', () => {
  let component: FileExplorerControlComponent;
  let fixture: ComponentFixture<FileExplorerControlComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileExplorerControlComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileExplorerControlComponent);
    component = fixture.componentInstance;
    component.entity = new FormGroup({ extraProperties: new FormGroup({}) });
    component.fields = { field: { description: '' } };
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
