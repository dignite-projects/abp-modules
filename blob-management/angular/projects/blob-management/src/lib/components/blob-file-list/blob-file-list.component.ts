import { CoreModule, ListService, PagedResultDto } from '@abp/ng.core';
import { ThemeSharedModule } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, OnInit, effect, inject, input, signal, untracked } from '@angular/core';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { FormatFileSizePipe } from '../../pipes/format-file-size.pipe';
import { BlobManagementContainerDto } from '../../proxy/containers';
import { BlobManagementDirectoryDto } from '../../proxy/directories';
import { BlobManagementFileDto, BlobManagementFileService } from '../../proxy/files';
import { ObjectUrlService } from '../../services/object-url.service';
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
  private readonly objectUrls = inject(ObjectUrlService);

  readonly container = input.required<BlobManagementContainerDto>();
  /** The directory whose files are listed; `null` for the top of the container. */
  readonly directory = input<BlobManagementDirectoryDto | null>(null);

  readonly data = signal<PagedResultDto<BlobManagementFileDto>>({ items: [], totalCount: 0 });

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
}
