import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FileExplorerViewComponent } from './file-explorer-view.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileExplorerViewComponent', () => {
  let component: FileExplorerViewComponent;
  let fixture: ComponentFixture<FileExplorerViewComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FileExplorerViewComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    })
    .compileComponents();
    
    fixture = TestBed.createComponent(FileExplorerViewComponent);
    component = fixture.componentInstance;
    component.fields = {};
    component.showInList = true;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
