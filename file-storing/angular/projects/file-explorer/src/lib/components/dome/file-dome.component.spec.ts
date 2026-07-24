import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FileDomeComponent } from './file-dome.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';

describe('FileDomeComponent', () => {
  let component: FileDomeComponent;
  let fixture: ComponentFixture<FileDomeComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileDomeComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileDomeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
