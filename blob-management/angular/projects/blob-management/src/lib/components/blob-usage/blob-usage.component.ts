import { CoreModule, ListService, PagedResultDto } from '@abp/ng.core';
import { ThemeSharedModule } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { FormatFileSizePipe } from '../../pipes/format-file-size.pipe';
import {
  BlobManagementClientUsageDto,
  BlobManagementUsageDto,
  BlobManagementUsageService,
  BlobManagementUserUsageDto,
} from '../../proxy/usage';

/** Which breakdown of the usage is shown. */
export type BlobManagementUsageView = 'containers' | 'users' | 'clients';

/**
 * What the tenant keeps in Blob Management: its stored bytes against its allocation, then by container, by user or by
 * client. Two measures, said apart on the page: stored bytes count each piece of content once; uploaded bytes - a
 * user's, a client's - count every file of theirs, so they do not add up to the stored bytes.
 */
@Component({
  selector: 'blob-usage',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './blob-usage.component.html',
  providers: [ListService],
  imports: [CoreModule, ThemeSharedModule, NgxDatatableModule, FormatFileSizePipe],
})
export class BlobManagementUsageComponent implements OnInit {
  readonly list = inject(ListService);
  private readonly usageService = inject(BlobManagementUsageService);

  readonly usage = signal<BlobManagementUsageDto | null>(null);
  readonly users = signal<PagedResultDto<BlobManagementUserUsageDto>>({ items: [], totalCount: 0 });
  readonly clients = signal<BlobManagementClientUsageDto[] | null>(null);
  readonly view = signal<BlobManagementUsageView>('containers');

  /** How much of the allocation is used, from 0 to 100; `null` when the tenant has no limit. */
  readonly usedPercent = computed(() => {
    const usage = this.usage();
    return usage?.storageSize ? Math.min(100, Math.round(((usage.storedBytes ?? 0) / usage.storageSize) * 100)) : null;
  });

  private usersHooked = false;

  ngOnInit(): void {
    this.usageService.get().subscribe(usage => this.usage.set(usage));
  }

  /** Shows a breakdown; the users and the clients are read when they are first asked for. */
  show(view: BlobManagementUsageView): void {
    this.view.set(view);

    if (view === 'users' && !this.usersHooked) {
      this.usersHooked = true;
      this.list.maxResultCount = 10;
      this.list
        .hookToQuery(query => this.usageService.getUsers({ skipCount: query.skipCount, maxResultCount: query.maxResultCount }))
        .subscribe(result => this.users.set(result));
    }

    if (view === 'clients' && this.clients() === null) {
      this.usageService.getClients({}).subscribe(result => this.clients.set(result.items ?? []));
    }
  }
}
