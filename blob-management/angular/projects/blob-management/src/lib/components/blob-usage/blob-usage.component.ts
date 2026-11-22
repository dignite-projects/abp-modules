import { CoreModule, ListService, PagedResultDto, PermissionService } from '@abp/ng.core';
import { PageComponent, PageToolbarContainerComponent } from '@abp/ng.components/page';
import { ThemeSharedModule } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, InjectionToken, OnInit, computed, inject, signal } from '@angular/core';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { eBlobManagementPolicyNames } from '@dignite/ng.blob-management/config';
import { FormatFileSizePipe } from '../../pipes/format-file-size.pipe';
import {
  BlobManagementClientUsageDto,
  BlobManagementTenantUsageDto,
  BlobManagementUsageDto,
  BlobManagementUsageService,
  BlobManagementUserUsageDto,
} from '@dignite/ng.blob-management/proxy';

/** Which breakdown of the usage is shown. */
export type BlobManagementUsageView = 'containers' | 'users' | 'clients' | 'tenants';

/** The paging of the tenants' breakdown: the users' has its own, so the two never share a page. */
const TENANT_USAGE_LIST = new InjectionToken<ListService>('BlobManagementTenantUsageList');

/**
 * The usage page: what the tenant keeps in Blob Management, its stored bytes against its allocation, then by container,
 * by user or by client - each of which adds up to the tenant's stored bytes - and, for the host, by tenant.
 * <p>
 * Each breakdown is read when it is asked for, and again when its refresh button is pressed, never on its own: the
 * containers' with the totals when the page opens (it is the first one shown), the others the first time they are
 * selected. The tenants' is there only for a caller who holds `BlobManagement.Containers.TenantUsages`, which is
 * defined for the host side only.
 */
@Component({
  selector: 'blob-usage',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './blob-usage.component.html',
  providers: [ListService, { provide: TENANT_USAGE_LIST, useClass: ListService }],
  imports: [CoreModule, ThemeSharedModule, NgxDatatableModule, PageComponent, PageToolbarContainerComponent, FormatFileSizePipe],
})
export class BlobManagementUsageComponent implements OnInit {
  /** The paging of the users' breakdown. */
  readonly list = inject(ListService);
  /** The paging of the tenants' breakdown. */
  readonly tenantList = inject(TENANT_USAGE_LIST);
  private readonly usageService = inject(BlobManagementUsageService);
  private readonly permissionService = inject(PermissionService);

  readonly usage = signal<BlobManagementUsageDto | null>(null);
  readonly users = signal<PagedResultDto<BlobManagementUserUsageDto>>({ items: [], totalCount: 0 });
  readonly clients = signal<BlobManagementClientUsageDto[] | null>(null);
  readonly tenants = signal<PagedResultDto<BlobManagementTenantUsageDto>>({ items: [], totalCount: 0 });
  readonly view = signal<BlobManagementUsageView>('containers');

  /** Whether the caller reads the usage of every tenant: only the host can hold the permission. */
  readonly canViewTenants = computed(() =>
    this.permissionService.getGrantedPolicy(eBlobManagementPolicyNames.TenantUsages),
  );

  /** How much of the allocation is used, from 0 to 100; `null` when the tenant has no limit. */
  readonly usedPercent = computed(() => {
    const usage = this.usage();
    return this.percentOf(usage?.storedBytes, usage?.storageSize);
  });

  private usersHooked = false;
  private tenantsHooked = false;

  ngOnInit(): void {
    this.loadUsage();
  }

  /** Shows a breakdown; the users, the clients and the tenants are read when they are first asked for. */
  show(view: BlobManagementUsageView): void {
    if (view === 'tenants' && !this.canViewTenants()) {
      return;
    }

    this.view.set(view);

    if (view === 'users' && !this.usersHooked) {
      this.usersHooked = true;
      this.list.maxResultCount = 10;
      this.list
        .hookToQuery(query => this.usageService.getUsers({ skipCount: query.skipCount, maxResultCount: query.maxResultCount }))
        .subscribe(result => this.users.set(result));
    }

    if (view === 'clients' && this.clients() === null) {
      this.loadClients();
    }

    if (view === 'tenants' && !this.tenantsHooked) {
      this.tenantsHooked = true;
      this.tenantList.maxResultCount = 10;
      this.tenantList
        .hookToQuery(query => this.usageService.getTenants({ skipCount: query.skipCount, maxResultCount: query.maxResultCount }))
        .subscribe(result => this.tenants.set(result));
    }
  }

  /**
   * Reads the breakdown shown again: the usage is computed from the files when it is asked for, so what has been stored
   * or deleted since shows only now. The containers' are read with the totals above them.
   */
  refresh(): void {
    switch (this.view()) {
      case 'containers':
        this.loadUsage();
        break;
      case 'users':
        this.list.getWithoutPageReset();
        break;
      case 'clients':
        this.loadClients();
        break;
      case 'tenants':
        this.tenantList.getWithoutPageReset();
        break;
    }
  }

  /** The share of an allocation a tenant uses, from 0 to 100; `null` when it has no limit. */
  percentOf(storedBytes: number | undefined, storageSize: number | null | undefined): number | null {
    return storageSize ? Math.min(100, Math.round(((storedBytes ?? 0) / storageSize) * 100)) : null;
  }

  private loadUsage(): void {
    this.usageService.get().subscribe(usage => this.usage.set(usage));
  }

  private loadClients(): void {
    this.usageService.getClients({}).subscribe(result => this.clients.set(result.items ?? []));
  }
}
