import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { ConfigStateService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { BlobManagementFileListComponent } from './blob-file-list.component';
import { BlobManagementFileService } from '../../proxy/files';

describe('BlobManagementFileListComponent', () => {
  let getList: ReturnType<typeof vi.fn>;
  let create: ReturnType<typeof vi.fn>;
  let toasterError: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<BlobManagementFileListComponent>;

  function picked(...names: string[]): Event {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: names.map(name => new File(['x'], name)) });
    return { target: input } as unknown as Event;
  }

  function container(extra: object = {}) {
    return { name: 'documents', maxFileSize: 10, allowDirectories: true, access: { view: true, uploadFiles: true }, ...extra };
  }

  function render(directory: unknown = null, containerDto: object = container(), selection = 'none') {
    getList = vi.fn(() => of({ items: [], totalCount: 0 }));
    create = vi.fn(() => of({}));
    toasterError = vi.fn();
    TestBed.configureTestingModule({
      imports: [BlobManagementFileListComponent, CoreTestingModule.withConfig()],
      providers: [
        { provide: BlobManagementFileService, useValue: { getList, create } },
        { provide: ConfirmationService, useValue: {} },
        { provide: ToasterService, useValue: { error: toasterError } },
      ],
    });
    // The signed-in user, whose own uploads the list lets them delete.
    const config = TestBed.inject(ConfigStateService);
    const getDeep = config.getDeep.bind(config);
    vi.spyOn(config, 'getDeep').mockImplementation(key => (key === 'currentUser.id' ? 'me' : getDeep(key)));
    fixture = TestBed.createComponent(BlobManagementFileListComponent);
    fixture.componentRef.setInput('container', containerDto);
    fixture.componentRef.setInput('directory', directory);
    fixture.componentRef.setInput('selection', selection);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('lists the files of the directory shown', () => {
    render({ id: 'd1', access: { view: true } });

    expect(getList).toHaveBeenCalledWith(expect.objectContaining({ containerName: 'documents', directoryId: 'd1' }));
  });

  it('takes what may be done from the place: the directory, or the top of the container', () => {
    expect(render(null).access().uploadFiles).toBe(true);
    TestBed.resetTestingModule();
    expect(render({ id: 'd1', access: { view: true, uploadFiles: false } }).access().uploadFiles).toBe(false);
  });

  it('renames and moves by the place, and never a file an application saved under its name', () => {
    const list = render({ id: 'd1', access: { view: true, updateFiles: true } });

    expect(list.canUpdate({ id: 'a', isAddressedByName: false })).toBe(true);
    expect(list.canUpdate({ id: 'b', isAddressedByName: true })).toBe(false);
    TestBed.resetTestingModule();
    expect(render({ id: 'd1', access: { view: true } }).canUpdate({ id: 'a' })).toBe(false);
  });

  it('deletes by the place, and lets the uploader delete their own file', () => {
    const list = render({ id: 'd1', access: { view: true } });

    expect(list.canDelete({ id: 'a', creatorId: 'someone-else' })).toBe(false);
    expect(list.canDelete({ id: 'b', creatorId: 'me' })).toBe(true);
    TestBed.resetTestingModule();
    expect(render({ id: 'd1', access: { view: true, deleteFiles: true } }).canDelete({ id: 'a', creatorId: 'someone-else' })).toBe(true);
  });

  it('sends the container, the directory and the file as a form', () => {
    const list = render({ id: 'd1', access: { view: true, uploadFiles: true } });
    const form = list.toFormData(new File(['abc'], 'a.txt'), 'documents', 'd1');

    expect(form.get('containerName')).toBe('documents');
    expect(form.get('directoryId')).toBe('d1');
    expect((form.get('file') as File).name).toBe('a.txt');
  });

  it('leaves out a file larger than the container takes', () => {
    const list = render(null, container({ maxFileSize: 2 }));
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [new File(['too big'], 'big.txt'), new File(['ok'], 'ok.txt')] });

    list.upload({ target: input } as unknown as Event);

    expect(toasterError).toHaveBeenCalledTimes(1);
    expect(create).toHaveBeenCalledTimes(1);
  });

  it('leaves out a file whose extension the container does not allow, and takes any when the list is empty', () => {
    const list = render(null, container({ allowedFileExtensions: ['.pdf'] }));

    list.upload(picked('a.PDF', 'b.exe'));

    expect(create).toHaveBeenCalledTimes(1);
    expect(((create.mock.calls[0][0] as FormData).get('file') as File).name).toBe('a.PDF');
    expect(toasterError).toHaveBeenCalledWith('BlobManagement::FileTypeNotAllowed', '', { messageLocalizationParams: ['b.exe'] });

    create.mockClear();
    toasterError.mockClear();
    fixture.componentRef.setInput('container', container({ allowedFileExtensions: [] }));
    fixture.detectChanges();

    list.upload(picked('a.PDF', 'b.exe'));

    expect(create).toHaveBeenCalledTimes(2);
    expect(toasterError).not.toHaveBeenCalled();
  });

  it('offers only the extensions the container allows, and no filter when it allows any', () => {
    const list = render(null);
    expect(list.accept()).toBeNull();
    expect(fixture.nativeElement.querySelector('input[type=file]').hasAttribute('accept')).toBe(false);

    fixture.componentRef.setInput('container', container({ allowedFileExtensions: ['.pdf', '.png'] }));
    fixture.detectChanges();

    expect(list.accept()).toBe('.pdf,.png');
    expect(fixture.nativeElement.querySelector('input[type=file]').getAttribute('accept')).toBe('.pdf,.png');
  });

  it('offers no upload where the place does not allow it', () => {
    render(null, container({ access: { view: true } }));

    expect(fixture.nativeElement.querySelector('input[type=file]')).toBeNull();
  });

  it('uploads every file into the directory shown when the upload started', () => {
    const list = render({ id: 'd1', access: { view: true, uploadFiles: true } });
    const first = new Subject<object>();
    create.mockImplementationOnce(() => first);

    list.upload(picked('a.txt', 'b.txt'));
    fixture.componentRef.setInput('directory', { id: 'd2', access: { view: true, uploadFiles: true } });
    fixture.detectChanges();
    first.next({});
    first.complete();

    expect(create).toHaveBeenCalledTimes(2);
    expect(create.mock.calls.map(([form]) => (form as FormData).get('directoryId'))).toEqual(['d1', 'd1']);
    expect(create.mock.calls.map(([form]) => (form as FormData).get('containerName'))).toEqual(['documents', 'documents']);
  });

  it('goes on after a file fails, and counts the failures in one message', () => {
    const list = render(null);
    create.mockImplementationOnce(() => throwError(() => new Error('refused')));

    list.upload(picked('a.txt', 'b.txt', 'c.txt'));

    expect(create).toHaveBeenCalledTimes(3);
    expect(toasterError).toHaveBeenCalledTimes(1);
    expect(toasterError).toHaveBeenCalledWith('BlobManagement::UploadFailed', '', { messageLocalizationParams: ['1', '3'] });
    expect(list.uploading()).toBe(false);
  });

  it('chooses several files in multiple selection', () => {
    const list = render(null, container(), 'multiple');

    list.toggle({ id: 'a' }, true);
    list.toggle({ id: 'b' }, true);
    expect(list.selected().map(file => file.id)).toEqual(['a', 'b']);
    expect(list.isSelected({ id: 'a' })).toBe(true);

    list.toggle({ id: 'a' }, false);
    expect(list.selected().map(file => file.id)).toEqual(['b']);
  });

  it('replaces the choice in single selection', () => {
    const list = render(null, container(), 'single');

    list.toggle({ id: 'a' }, true);
    list.toggle({ id: 'b' }, true);
    expect(list.selected().map(file => file.id)).toEqual(['b']);
  });
});
