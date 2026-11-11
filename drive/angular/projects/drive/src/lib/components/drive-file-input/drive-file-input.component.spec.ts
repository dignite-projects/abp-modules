import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ReplaceableComponentsService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { ToasterService } from '@abp/ng.theme.shared';
import { DriveFileInputComponent, DriveFileInputReplaceableData } from './drive-file-input.component';
import { eDriveComponents } from '../../enums/components';
import { DriveContainerService } from '../../proxy/containers';
import { DriveFileDto, DriveFileService } from '../../proxy/files';

@Component({ selector: 'drive-test-library-picker', template: `<span class="library-picker">{{ data.inputs.containerName }}</span>` })
class LibraryPickerComponent {
  readonly data = inject<DriveFileInputReplaceableData>('REPLACEABLE_DATA' as never);
}

describe('DriveFileInputComponent', () => {
  let create: ReturnType<typeof vi.fn>;
  let remove: ReturnType<typeof vi.fn>;
  let toasterError: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<DriveFileInputComponent>;
  let changes: DriveFileDto[][];

  function chosen(...names: string[]): Event {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: names.map(name => new File(['x'], name)) });
    return { target: input } as unknown as Event;
  }

  function render(multiple = false, container: Partial<{ maxFileSize: number; allowedFileExtensions: string[] }> = {}) {
    let id = 0;
    create = vi.fn((form: FormData) => of({ id: `f${++id}`, name: (form.get('file') as File).name, size: 1 }));
    remove = vi.fn();
    toasterError = vi.fn();
    TestBed.configureTestingModule({
      imports: [DriveFileInputComponent, CoreTestingModule.withConfig()],
      providers: [
        { provide: DriveFileService, useValue: { create, delete: remove } },
        { provide: DriveContainerService, useValue: { get: vi.fn(() => of({ name: 'attachments', ...container })) } },
        { provide: ToasterService, useValue: { error: toasterError } },
      ],
    });
    fixture = TestBed.createComponent(DriveFileInputComponent);
    fixture.componentRef.setInput('containerName', 'attachments');
    fixture.componentRef.setInput('multiple', multiple);
    changes = [];
    fixture.componentInstance.registerOnChange(value => changes.push(value));
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('uploads each file chosen into the container and attaches it', () => {
    const input = render(true);

    input.upload(chosen('a.pdf', 'b.pdf'));

    expect(create.mock.calls.map(([form]) => (form as FormData).get('containerName'))).toEqual(['attachments', 'attachments']);
    expect(input.value().map(file => file.name)).toEqual(['a.pdf', 'b.pdf']);
    expect(changes.at(-1)?.map(file => file.id)).toEqual(['f1', 'f2']);
  });

  it('replaces the file attached when it takes one file', () => {
    const input = render(false);
    input.writeValue([{ id: 'old', name: 'old.pdf' }]);

    input.upload(chosen('new.pdf'));

    expect(input.value().map(file => file.id)).toEqual(['f1']);
  });

  it('leaves out a file the container would refuse', () => {
    const input = render(true, { maxFileSize: 0, allowedFileExtensions: ['.pdf'] });

    input.upload(chosen('a.PDF', 'b.exe'));

    expect(create).toHaveBeenCalledTimes(1);
    expect(toasterError).toHaveBeenCalledWith('Drive::FileTypeNotAllowed', '', { messageLocalizationParams: ['b.exe'] });
    expect(input.accept()).toBe('.pdf');
  });

  it('goes on after a failed upload and counts the failures', () => {
    const input = render(true);
    create.mockImplementationOnce(() => throwError(() => new Error('refused')));

    input.upload(chosen('a.pdf', 'b.pdf'));

    expect(input.value().map(file => file.name)).toEqual(['b.pdf']);
    expect(toasterError).toHaveBeenCalledWith('Drive::UploadFailed', '', { messageLocalizationParams: ['1', '2'] });
  });

  it('removes a file from the value without deleting it from the Drive', () => {
    const input = render(true);
    input.writeValue([{ id: 'a' }, { id: 'b' }]);

    input.remove({ id: 'a' });

    expect(changes.at(-1)).toEqual([{ id: 'b' }]);
    expect(remove).not.toHaveBeenCalled();
  });

  it('lets a component registered under its key take its place and set the value', () => {
    TestBed.configureTestingModule({});
    render(true);
    TestBed.inject(ReplaceableComponentsService).add({ key: eDriveComponents.FileInput, component: LibraryPickerComponent });
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.library-picker')?.textContent).toBe('attachments');
    expect(element.querySelector('button')).toBeNull();

    const picker = fixture.debugElement.query(debug => debug.componentInstance instanceof LibraryPickerComponent)
      .componentInstance as LibraryPickerComponent;
    picker.data.inputs.value = [{ id: 'from-library' }];

    expect(fixture.componentInstance.value()).toEqual([{ id: 'from-library' }]);
    expect(changes.at(-1)).toEqual([{ id: 'from-library' }]);
  });
});
