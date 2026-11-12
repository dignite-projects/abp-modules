import { CoreModule, ReplaceableComponents } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  computed,
  effect,
  forwardRef,
  inject,
  input,
  model,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { EMPTY, catchError, concatMap, finalize, from, tap } from 'rxjs';
import { eBlobManagementComponents } from '../../enums/components';
import { FormatFileSizePipe } from '../../pipes/format-file-size.pipe';
import { BlobManagementContainerDto, BlobManagementContainerService } from '../../proxy/containers';
import { BlobManagementFileDto, BlobManagementFileService } from '../../proxy/files';
import { BlobManagementFileUrlService } from '../../services/blob-file-url.service';

/**
 * What a component registered under `eBlobManagementComponents.FileInput` receives as ABP's `REPLACEABLE_DATA` inputs.
 * `value` is two-way: assigning it reports the new list to the form.
 */
export interface BlobManagementFileInputData {
  value: BlobManagementFileDto[];
  containerName: string | null;
  multiple: boolean;
  disabled: boolean;
}

/** What a component registered under `eBlobManagementComponents.FileInput` injects as `'REPLACEABLE_DATA'`. */
export type BlobManagementFileInputReplaceableData = ReplaceableComponents.ReplaceableTemplateData<
  BlobManagementFileInputData,
  { valueChange: EventEmitter<BlobManagementFileDto[]> }
>;

/**
 * Attaches files to a form: uploads the files the user chooses into a container of Blob Management, shows the files
 * attached and lets the user remove them. Its value is the list of the files attached (one at most unless
 * `multiple`), bound with `formControlName`, `ngModel` or `[(value)]`.
 *
 * Removing a file takes it out of the value only; the file stays in Blob Management, since the value may not have been saved
 * yet. An application replaces what is shown, for example with a picker over a file library, by registering its own
 * component under `eBlobManagementComponents.FileInput`; it injects `BlobManagementFileInputReplaceableData` and the form binding stays
 * here.
 */
@Component({
  selector: 'blob-file-input',
  changeDetection: ChangeDetectionStrategy.Eager,
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => BlobManagementFileInputComponent), multi: true },
  ],
  template: `
    <ng-container
      *abpReplaceableTemplate="{
        componentKey: componentKey,
        inputs: {
          value: { value: value(), twoWay: true },
          containerName: { value: containerName() },
          multiple: { value: multiple() },
          disabled: { value: disabled() }
        },
        outputs: { valueChange: setValue }
      }"
    >
      <input
        #picker
        type="file"
        class="d-none"
        [attr.accept]="accept()"
        [multiple]="multiple()"
        (change)="upload($event)"
      />
      <button
        type="button"
        class="btn btn-sm btn-outline-primary"
        [disabled]="disabled() || uploading() || !containerName()"
        (click)="picker.click()"
      >
        @if (uploading()) {
          <span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>
        } @else {
          <i class="fa fa-upload me-1" aria-hidden="true"></i>
        }
        {{ 'BlobManagement::Upload' | abpLocalization }}
      </button>

      @if (value().length > 0) {
        <ul class="list-group mt-2">
          @for (file of value(); track file.id) {
            <li class="list-group-item d-flex align-items-center gap-2">
              <i class="fa fa-file text-muted" aria-hidden="true"></i>
              @if (file.url) {
                <a class="flex-grow-1 text-break" [href]="urls.toAbsolute(file.url)" target="_blank" rel="noopener">{{
                  file.name
                }}</a>
              } @else {
                <span class="flex-grow-1 text-break">{{ file.name }}</span>
              }
              <small class="text-muted">{{ file.size | blobFileSize }}</small>
              <button
                type="button"
                class="btn btn-sm btn-outline-secondary"
                [disabled]="disabled()"
                [attr.aria-label]="'BlobManagement::RemoveFile' | abpLocalization: file.name ?? ''"
                (click)="remove(file)"
              >
                <i class="fa fa-times" aria-hidden="true"></i>
              </button>
            </li>
          }
        </ul>
      } @else {
        <div class="text-muted small mt-2">{{ 'BlobManagement::NoFileSelected' | abpLocalization }}</div>
      }
    </ng-container>
  `,
  imports: [CoreModule, FormatFileSizePipe],
})
export class BlobManagementFileInputComponent implements ControlValueAccessor {
  private readonly fileService = inject(BlobManagementFileService);
  private readonly containerService = inject(BlobManagementContainerService);
  private readonly toaster = inject(ToasterService);
  private readonly fileSize = new FormatFileSizePipe();
  readonly urls = inject(BlobManagementFileUrlService);

  readonly componentKey = eBlobManagementComponents.FileInput;

  /** The container the files are uploaded into: a user container the signed-in user may upload to. */
  readonly containerName = input<string | null>(null);
  /** Whether several files may be attached; otherwise an upload replaces the file attached. */
  readonly multiple = input(false);
  /** The files attached. */
  readonly value = model<BlobManagementFileDto[]>([]);

  readonly disabled = signal(false);
  readonly uploading = signal(false);
  /** The container's limits, for checking a file before it is sent; Blob Management checks again on upload. */
  readonly container = signal<BlobManagementContainerDto | null>(null);

  /** The file input's `accept`: the container's allowed extensions; `null` - no attribute - for any. */
  readonly accept = computed(() => {
    const extensions = this.container()?.allowedFileExtensions ?? [];
    return extensions.length > 0 ? extensions.join(',') : null;
  });

  private onChange: (value: BlobManagementFileDto[]) => void = () => {};
  private onTouched: () => void = () => {};

  constructor() {
    effect(onCleanup => {
      const name = this.containerName();
      this.container.set(null);
      if (!name) {
        return;
      }

      // Without the limits the BlobManagement's own checks on upload still apply, so a failure here is not shown.
      const subscription = this.containerService
        .get(name, { skipHandleError: true })
        .pipe(catchError(() => EMPTY))
        .subscribe(container => this.container.set(container));
      onCleanup(() => subscription.unsubscribe());
    });
  }

  /** Sets the files attached and reports them to the form; a replacement component's `value` writes here. */
  readonly setValue = (files: unknown): void => {
    const value = (files as BlobManagementFileDto[] | null | undefined) ?? [];
    this.value.set(value);
    this.onChange(value);
    this.onTouched();
  };

  /**
   * Uploads the files chosen, one after another, leaving out any the container would refuse for its size or its type.
   * A file that fails does not stop the others; each one uploaded is attached as soon as it is stored.
   */
  upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const containerName = this.containerName();
    const chosen = Array.from(input.files ?? []);
    input.value = '';
    if (!containerName) {
      return;
    }

    const files = chosen.filter(file => this.accepts(file));
    if (files.length === 0) {
      return;
    }

    // ABP's error handler has shown each failure already; the batch goes on and one toast counts them at the end.
    let failed = 0;
    this.uploading.set(true);
    from(files)
      .pipe(
        concatMap(file =>
          this.fileService.create(this.toFormData(file, containerName)).pipe(
            tap(uploaded => this.setValue(this.multiple() ? [...this.value(), uploaded] : [uploaded])),
            catchError(() => {
              failed++;
              return EMPTY;
            }),
          ),
        ),
        finalize(() => {
          this.uploading.set(false);
          if (failed > 0) {
            this.toaster.error('BlobManagement::UploadFailed', '', {
              messageLocalizationParams: [String(failed), String(files.length)],
            });
          }
        }),
      )
      .subscribe();
  }

  /** Takes the file out of the value; the file itself stays in the BlobManagement. */
  remove(file: BlobManagementFileDto): void {
    this.setValue(this.value().filter(item => item.id !== file.id));
  }

  /** What the BlobManagement's form upload takes: the container and the file. */
  toFormData(file: File, containerName: string): FormData {
    const form = new FormData();
    form.append('containerName', containerName);
    form.append('file', file, file.name);
    return form;
  }

  writeValue(value: BlobManagementFileDto[] | null | undefined): void {
    this.value.set(value ?? []);
  }

  registerOnChange(fn: (value: BlobManagementFileDto[]) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  private accepts(file: File): boolean {
    const maxFileSize = this.container()?.maxFileSize ?? 0;
    if (maxFileSize > 0 && file.size > maxFileSize) {
      this.toaster.error('BlobManagement::FileTooLarge', '', {
        messageLocalizationParams: [file.name, this.fileSize.transform(maxFileSize)],
      });
      return false;
    }

    const extensions = this.container()?.allowedFileExtensions ?? [];
    const name = file.name.toLowerCase();
    if (extensions.length > 0 && !extensions.some(extension => name.endsWith(extension.toLowerCase()))) {
      this.toaster.error('BlobManagement::FileTypeNotAllowed', '', { messageLocalizationParams: [file.name] });
      return false;
    }

    return true;
  }
}
