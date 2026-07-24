import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FileEditComponent } from './file-edit.component';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { ObjectUrlService } from '../../services/object-url.service';

describe('FileEditComponent', () => {
  let component: FileEditComponent;
  let fixture: ComponentFixture<FileEditComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileEditComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()]
    });
    fixture = TestBed.createComponent(FileEditComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should finish preparing every selected file before returning', async () => {
    const imageUrlService = TestBed.inject(ObjectUrlService);
    imageUrlService.get = () => 'blob:test-preview';
    const files = [new File(['content'], 'test.txt', { type: 'text/plain' })] as any[];

    const result = await component.setfileSizeUnits(files);

    expect(result).toBe(files);
    expect(files[0].src).toBe('blob:test-preview');
    expect(files[0].fileSize).toBeTruthy();
  });
});
