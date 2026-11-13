import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { BlobManagementFileListComponent } from './blob-file-list.component';
import { BlobManagementFileService } from '../../proxy/files';

describe('BlobManagementFileListComponent', () => {
  let getList: ReturnType<typeof vi.fn>;
  let getContent: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<BlobManagementFileListComponent>;

  function render(directory: unknown = null) {
    getList = vi.fn(() => of({ items: [{ id: 'f1', name: 'report.pdf', size: 3 }], totalCount: 1 }));
    getContent = vi.fn(() => of(new Blob(['x'])));
    TestBed.configureTestingModule({
      imports: [BlobManagementFileListComponent, CoreTestingModule.withConfig()],
      providers: [{ provide: BlobManagementFileService, useValue: { getList, getContent } }],
    });
    fixture = TestBed.createComponent(BlobManagementFileListComponent);
    fixture.componentRef.setInput('container', { name: 'documents', allowDirectories: true, access: { view: true } });
    fixture.componentRef.setInput('directory', directory);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('lists the files of the directory shown', () => {
    render({ id: 'd1' });

    expect(getList).toHaveBeenCalledWith(expect.objectContaining({ containerName: 'documents', directoryId: 'd1' }));
  });

  it('lists the top of the container without a directory', () => {
    render();

    expect(getList).toHaveBeenCalledWith(expect.objectContaining({ containerName: 'documents', directoryId: null }));
  });

  it('lists again when the directory shown changes', () => {
    render();
    getList.mockClear();

    fixture.componentRef.setInput('directory', { id: 'd2' });
    fixture.detectChanges();

    expect(getList).toHaveBeenCalledWith(expect.objectContaining({ directoryId: 'd2' }));
  });

  it('downloads a file through the file API', () => {
    const list = render();

    list.download({ id: 'f1', name: 'report.pdf' });

    expect(getContent).toHaveBeenCalledWith('f1');
  });

  it('offers nothing that changes a file', () => {
    render();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('input[type=file]')).toBeNull();
    expect(element.querySelector('[ngbDropdown]')).toBeNull();
  });
});
