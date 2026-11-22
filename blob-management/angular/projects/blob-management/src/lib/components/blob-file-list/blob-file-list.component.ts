import { AbpWindowService, CoreModule, ListService, PagedResultDto } from '@abp/ng.core';
import { ThemeSharedModule } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { FormatFileSizePipe } from '../../pipes/format-file-size.pipe';
import {
  BlobManagementContainerDto,
  BlobManagementDirectoryDto,
  BlobManagementFileDto,
  BlobManagementFileService,
} from '@dignite/ng.blob-management/proxy';
import { BlobManagementFilePreviewComponent } from '../blob-file-preview/blob-file-preview.component';

/**
 * The files of one directory, or of the top of a container, for whoever holds the management permission: listed a
 * page at a time, searched and downloaded. Nothing here stores, renames, moves or deletes a file - a file is its
 * business system's, and the business record that refers to it would be left pointing at nothing.
 */
@Component({
  selector: 'blob-file-list',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './blob-file-list.component.html',
  providers: [ListService],
  imports: [CoreModule, ThemeSharedModule, NgxDatatableModule, FormatFileSizePipe, BlobManagementFilePreviewComponent],
})
export class BlobManagementFileListComponent implements OnInit {
  readonly list = inject(ListService);
  private readonly fileService = inject(BlobManagementFileService);
  private readonly window = inject(AbpWindowService);

  readonly container = input.required<BlobManagementContainerDto>();
  /** The directory whose files are listed; `null` for the top of the container. */
  readonly directory = input<BlobManagementDirectoryDto | null>(null);

  /**
   * Which container is listed. The container's object changes when what the caller may do with it does; the files
   * listed change only with its name.
   */
  readonly containerName = computed(() => this.container().name ?? '');

  readonly data = signal<PagedResultDto<BlobManagementFileDto>>({ items: [], totalCount: 0 });

  constructor() {
    effect(() => {
      this.containerName();
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
          containerName: this.containerName(),
          directoryId: this.directory()?.id ?? null,
        }),
      )
      .subscribe(result => this.data.set(result));
  }

  search(filter: string): void {
    this.list.filter = filter;
  }

  /** Saves the file's bytes through the file API - a file that is not public has no address. */
  download(file: BlobManagementFileDto): void {
    if (!file.id) {
      return;
    }

    this.fileService.getContent(file.id).subscribe(blob => this.window.downloadBlob(blob, file.name ?? 'file'));
  }
}
