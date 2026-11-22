import { CoreModule, PermissionService } from '@abp/ng.core';
import { PageComponent, PageToolbarContainerComponent } from '@abp/ng.components/page';
import { ResourcePermissionManagementComponent } from '@abp/ng.permission-management';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { eBlobManagementPolicyNames } from '@dignite/ng.blob-management/config';
import { eBlobManagementResourceNames } from '../blob-management-permissions';
import { BlobManagementContainerDto, BlobManagementContainerService } from '@dignite/ng.blob-management/proxy';
import { BlobManagementBrowserComponent } from './blob-browser/blob-browser.component';

/**
 * The files page, for whoever holds `BlobManagement.Containers`: one container at a time, its directories and the files
 * in them. What the tenant stores is the usage page's. The files are read here and nothing more: a file is stored,
 * renamed, moved and deleted by the business system that keeps it. Directories are kept here, by the permissions on the
 * container, which a user who manages grants opens from here.
 */
@Component({
  selector: 'blob-management-files',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-page [title]="'BlobManagement::Files' | abpLocalization">
      @if (canManagePermissions() && selected()) {
        <abp-page-toolbar-container class="col">
          <div class="d-flex justify-content-lg-end">
            <button type="button" class="btn btn-sm btn-outline-secondary" (click)="permissionsOpen.set(true)">
              <i class="fa fa-user-lock me-1" aria-hidden="true"></i>{{ 'BlobManagement::Permissions' | abpLocalization }}
            </button>
          </div>
        </abp-page-toolbar-container>
      }
    <div class="card">
      <div class="card-body">
        @if (loaded() && containers().length === 0) {
          <p class="text-muted mb-0">{{ 'BlobManagement::NoContainers' | abpLocalization }}</p>
        }
        @if (containers().length > 0) {
          <ul class="nav nav-tabs mb-3">
            @for (container of containers(); track container.name) {
              <li class="nav-item">
                <button
                  type="button"
                  class="nav-link"
                  [class.active]="container.name === selected()?.name"
                  (click)="selected.set(container)"
                >
                  {{ container.name }}
                </button>
              </li>
            }
          </ul>
        }
        @if (selected(); as container) {
          <blob-browser [container]="container" />
          @if (permissionsOpen()) {
            <abp-resource-permission-management
              [resourceName]="containerResourceName"
              [resourceKey]="container.name ?? ''"
              [resourceDisplayName]="container.name ?? ''"
              [visible]="true"
              (visibleChange)="onPermissionsVisibleChange($event)"
            />
          }
        }
      </div>
    </div>
    </abp-page>
  `,
  imports: [
    CoreModule,
    PageComponent,
    PageToolbarContainerComponent,
    BlobManagementBrowserComponent,
    ResourcePermissionManagementComponent,
  ],
})
export class BlobManagementFilesComponent {
  private readonly containerService = inject(BlobManagementContainerService);
  private readonly permissionService = inject(PermissionService);

  readonly containers = signal<BlobManagementContainerDto[]>([]);
  readonly selected = signal<BlobManagementContainerDto | null>(null);
  readonly loaded = signal(false);
  readonly permissionsOpen = signal(false);

  readonly containerResourceName = eBlobManagementResourceNames.Container;
  /** Whether the caller manages grants: ABP's dialog checks the same standard permission. */
  readonly canManagePermissions = computed(() =>
    this.permissionService.getGrantedPolicy(eBlobManagementPolicyNames.ManagePermissions),
  );

  constructor() {
    this.load();
  }

  /**
   * The grants on a container are changed in the dialog, and what the caller may do with its directories comes with
   * the container: once the dialog closes, the containers are read again.
   */
  onPermissionsVisibleChange(visible: boolean): void {
    this.permissionsOpen.set(visible);
    if (!visible) {
      this.load();
    }
  }

  /** Reads the containers and shows the one shown before, or the first. */
  private load(): void {
    this.containerService.getList().subscribe(result => {
      const containers = result.items ?? [];
      const shown = this.selected();

      this.containers.set(containers);
      this.selected.set(containers.find(container => container.name === shown?.name) ?? containers[0] ?? null);
      this.loaded.set(true);
    });
  }
}
