import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { ToasterService } from '@abp/ng.theme.shared';
import { BlobManagementFileInputComponent } from './blob-file-input.component';
import { BlobManagementContainerService } from '../../proxy/containers';
import { BlobManagementFileService } from '../../proxy/files';

describe('BlobManagementFileInputComponent', () => {
  let create: ReturnType<typeof vi.fn>;
  let toasterError: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<BlobManagementFileInputComponent>;

  function picked(...names: string[]): Event {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: names.map(name => new File(['x'], name)) });
    return { target: input } as unknown as Event;
  }

  function render(container: object | null = { name: 'media', maxFileSize: 10, access: { view: false } }, multiple = false) {
    let next = 0;
    create = vi.fn((form: FormData) => of({ id: 'f' + ++next, name: (form.get('file') as File).name }));
    toasterError = vi.fn();
    TestBed.configureTestingModule({
      imports: [BlobManagementFileInputComponent, CoreTestingModule.withConfig()],
      providers: [
        { provide: BlobManagementFileService, useValue: { create } },
        {
          provide: BlobManagementContainerService,
          useValue: { get: () => (container ? of(container) : throwError(() => new Error('not found'))), getList: () => of({ items: [] }) },
        },
        { provide: ToasterService, useValue: { error: toasterError } },
      ],
    });
    fixture = TestBed.createComponent(BlobManagementFileInputComponent);
    fixture.componentRef.setInput('containerName', 'media');
    fixture.componentRef.setInput('multiple', multiple);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('attaches the file uploaded, and reports it to the form', () => {
    const input = render();
    const changes: unknown[] = [];
    input.registerOnChange(value => changes.push(value));

    input.upload(picked('a.png'));

    expect((create.mock.calls[0][0] as FormData).get('containerName')).toBe('media');
    expect(input.value().map(file => file.name)).toEqual(['a.png']);
    expect(changes).toEqual([[{ id: 'f1', name: 'a.png' }]]);
  });

  it('keeps one file unless several may be attached', () => {
    const single = render();
    single.upload(picked('a.png', 'b.png'));
    expect(single.value().map(file => file.name)).toEqual(['b.png']);

    TestBed.resetTestingModule();
    const several = render(undefined, true);
    several.upload(picked('a.png', 'b.png'));
    expect(several.value().map(file => file.name)).toEqual(['a.png', 'b.png']);
  });

  it('leaves out what the container would refuse, by its size or its type', () => {
    const input = render({ name: 'media', maxFileSize: 2, allowedFileExtensions: ['.png'], access: {} });

    input.upload(picked('a.exe'));
    expect(create).not.toHaveBeenCalled();
    expect(toasterError).toHaveBeenCalledWith('BlobManagement::FileTypeNotAllowed', '', { messageLocalizationParams: ['a.exe'] });

    const big = document.createElement('input');
    Object.defineProperty(big, 'files', { value: [new File(['too big'], 'big.png')] });
    input.upload({ target: big } as unknown as Event);
    expect(create).not.toHaveBeenCalled();
    expect(toasterError).toHaveBeenCalledTimes(2);
  });

  it('removes a file from the value only', () => {
    const input = render(undefined, true);
    input.writeValue([{ id: 'a' }, { id: 'b' }]);

    input.remove({ id: 'a' });

    expect(input.value().map(file => file.id)).toEqual(['b']);
  });

  it('offers picking from the container only to whoever may view it', () => {
    render({ name: 'media', access: { view: false } });
    expect(fixture.nativeElement.querySelector('.blob-file-input-pick')).toBeNull();

    TestBed.resetTestingModule();
    render({ name: 'media', access: { view: true } });
    expect(fixture.nativeElement.querySelector('.blob-file-input-pick')).not.toBeNull();
  });

  it('still uploads when the container could not be read', () => {
    const input = render(null);

    input.upload(picked('a.png'));

    expect(create).toHaveBeenCalledTimes(1);
  });

  it('is disabled with its form control', () => {
    const input = render();
    input.setDisabledState(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.blob-file-input-upload').disabled).toBe(true);
  });
});
