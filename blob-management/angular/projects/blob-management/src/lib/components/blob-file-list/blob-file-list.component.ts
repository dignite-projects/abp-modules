import { ConfigStateService, CoreModule, ListService, PagedResultDto } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  effect,
  inject,
  input,
  model,
  signal,
  untracked,
} from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { EMPTY, catchError, concatMap, finalize, from } from 'rxjs';
import { FormatFileSizePipe } from '../../pipes/format-file-size.pipe';
import { BlobManagementContainerDto } from '../../proxy/containers';
import { BlobManagementDirectoryDto } from '../../proxy/directories';
import { BlobManagementFileDto, BlobManagementFileService } from '../../proxy/files';
import { BlobManagementAccessDto } from '../../proxy/models';
import { ObjectUrlService } from '../../services/object-url.service';
import { BlobManagementFileMoveModalComponent } from '../blob-file-move/blob-file-move-modal.component';
import { BlobManagementFilePreviewComponent } from '../blob-file-preview/blob-file-preview.component';

/** How many files a list lets the user choose. */
export type BlobManagementFileSelection = 'none' | 'single' | 'multiple';

/**
 * The files of one directory, or of the top of a container: listed a page at a time, uploaded into, downloaded,
 * renamed, moved to another directory and deleted. What is offered is the access of the place - worked out once by the
 * server, for the place - plus two things each file says for itself: its uploader may delete it, and a file an
 * application saved under its name is neither renamed nor moved. With `selection`, files can be chosen as well (the
 * file picker), across directories and pages.
 */
@Component({
  selector: 'blob-file-list',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './blob-file-list.component.html',
  providers: [ListService, FormatFileSizePipe],
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    NgbDropdownModule,
    NgxDatatableModule,
    FormatFileSizePipe,
    BlobManagementFilePreviewComponent,
    BlobManagementFileMoveModalComponent,
  ],
})
export class BlobManagementFileListComponent implements OnInit {
  readonly list = inject(ListService);
  private readonly fileService = inject(BlobManagementFileService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly objectUrls = inject(ObjectUrlService);
  private readonly fileSize = inject(FormatFileSizePipe);
  private readonly fb = inject(FormBuilder);
  private readonly config = inject(ConfigStateService);

  readonly container = input.required<BlobManagementContainerDto>();
  /** The directory whose files are listed; `null` for the top of the container. */
  readonly directory = input<BlobManagementDirectoryDto | null>(null);
  /** Whether files can be chosen, and how many. */
  readonly selection = input<BlobManagementFileSelection>('none');
  /** The files chosen, kept across directories and pages. */
  readonly selected = model<BlobManagementFileDto[]>([]);

  readonly data = signal<PagedResultDto<BlobManagementFileDto>>({ items: [], totalCount: 0 });
  readonly uploading = signal(false);

  /** What the caller may do here: the directory's access, or the container's at its top. */
  readonly access = computed<BlobManagementAccessDto>(() => this.directory()?.access ?? this.container().access ?? {});

  /** The file input's `accept`: the container's allowed extensions (each with its dot); `null` - no attribute - for any. */
  readonly accept = computed(() => {
    const extensions = this.container().allowedFileExtensions ?? [];
    return extensions.length > 0 ? extensions.join(',') : null;
  });

  /** The file the move dialog moves. */
  readonly moving = signal<BlobManagementFileDto | null>(null);

  renameOpen = false;
  renameBusy = false;
  renameForm: FormGroup | undefined;
  private renaming: BlobManagementFileDto | undefined;

  constructor() {
    effect(() => {
      this.container();
      this.directory();
      untracked(() => {
        if (this.list.page === 0) {
          this.list.get();
        } else {
          this.list.page = 0;
        }
      });
    });
  }

  ngOnInit(): void {
    this.list.maxResultCount = 20;
    this.list
      .hookToQuery(query =>
        this.fileService.getList({
          ...query,
          containerName: this.container().name ?? '',
          directoryId: this.directory()?.id ?? null,
        }),
      )
      .subscribe(result => this.data.set(result));
  }

  search(filter: string): void {
    this.list.filter = filter;
  }

  /** Renaming and moving: the place's permission, and never a file an application reads back by its name. */
  canUpdate(file: BlobManagementFileDto): boolean {
    return !!this.access().updateFiles && !file.isAddressedByName;
  }

  /** Deleting: the place's permission, or having uploaded the file. */
  canDelete(file: BlobManagementFileDto): boolean {
    const userId = this.config.getDeep('currentUser.id') as string | undefined;
    return !!this.access().deleteFiles || (!!userId && file.creatorId === userId);
  }

  isSelected(file: BlobManagementFileDto): boolean {
    return this.selected().some(item => item.id === file.id);
  }

  /** Chooses or unchooses a file: in single selection, choosing one replaces the other. */
  toggle(file: BlobManagementFileDto, checked: boolean): void {
    if (this.selection() === 'single') {
      this.selected.set(checked ? [file] : []);
      return;
    }

    const others = this.selected().filter(item => item.id !== file.id);
    this.selected.set(checked ? [...others, file] : others);
  }

  /**
   * Uploads the files picked, one after another, into the container and directory shown when the upload started,
   * leaving out any the container would refuse for its size or its type. A file that fails does not stop the others.
   */
  upload(event: Event): void {
    const input = event.target as HTMLInputElement;
    const picked = Array.from(input.files ?? []);
    input.value = '';

    const maxFileSize = this.container().maxFileSize ?? 0;
    const allowedExtensions = this.container().allowedFileExtensions ?? [];
    const files = picked.filter(file => {
      if (maxFileSize > 0 && file.size > maxFileSize) {
        this.toaster.error('BlobManagement::FileTooLarge', '', {
          messageLocalizationParams: [file.name, this.fileSize.transform(maxFileSize)],
        });
        return false;
      }

      const name = file.name.toLowerCase();
      if (allowedExtensions.length > 0 && !allowedExtensions.some(extension => name.endsWith(extension.toLowerCase()))) {
        this.toaster.error('BlobManagement::FileTypeNotAllowed', '', {
          messageLocalizationParams: [file.name],
        });
        return false;
      }

      return true;
    });

    if (files.length === 0) {
      return;
    }

    // Where the user clicked Upload: the user may move to another container or directory while the batch runs.
    const containerName = this.container().name ?? '';
    const directoryId = this.directory()?.id ?? null;
    // ABP's error handler has shown each failure already; the batch goes on and one toast counts them at the end.
    let failed = 0;

    this.uploading.set(true);
    from(files)
      .pipe(
        concatMap(file =>
          this.fileService.create(this.toFormData(file, containerName, directoryId)).pipe(
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

          // The list shown now, wherever the user is: the files went where the batch started.
          this.list.get();
        }),
      )
      .subscribe();
  }

  /** What the form upload takes: the container, the directory and the file. */
  toFormData(file: File, containerName: string, directoryId: string | null): FormData {
    const form = new FormData();
    form.append('containerName', containerName);
    if (directoryId) {
      form.append('directoryId', directoryId);
    }

    form.append('file', file, file.name);
    return form;
  }

  /** Saves the file's bytes through the file API - a file of a container that is not public has no address. */
  download(file: BlobManagementFileDto): void {
    if (!file.id) {
      return;
    }

    this.fileService.getContent(file.id).subscribe(blob => {
      const url = this.objectUrls.get(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = file.name ?? 'file';
      link.click();
      setTimeout(() => this.objectUrls.revoke(url), 1000);
    });
  }

  openRename(file: BlobManagementFileDto): void {
    this.renaming = file;
    this.renameForm = this.fb.group({ name: [file.name ?? '', [Validators.required, Validators.maxLength(256)]] });
    this.renameOpen = true;
  }

  saveRename(): void {
    if (!this.renaming?.id || !this.renameForm || this.renameForm.invalid || this.renameBusy) {
      return;
    }

    this.renameBusy = true;
    this.fileService
      .update(this.renaming.id, { name: (this.renameForm.value.name as string).trim() })
      .pipe(finalize(() => (this.renameBusy = false)))
      .subscribe(() => {
        this.renameOpen = false;
        this.list.get();
      });
  }

  delete(file: BlobManagementFileDto): void {
    this.confirmation
      .warn('BlobManagement::DeleteFileConfirmation', 'AbpUi::AreYouSure', { messageLocalizationParams: [file.name ?? ''] })
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm && file.id) {
          this.fileService.delete(file.id).subscribe(() => this.list.get());
        }
      });
  }
}
