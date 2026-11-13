import { CoreModule, PermissionService } from '@abp/ng.core';
import { ResourcePermissionManagementComponent } from '@abp/ng.permission-management';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { eBlobManagementPolicyNames, eBlobManagementResourceNames } from '../blob-management-permissions';
import { BlobManagementContainerDto, BlobManagementContainerService } from '../proxy/containers';
import { BlobManagementBrowserComponent } from './blob-browser/blob-browser.component';
import { BlobManagementUsageComponent } from './blob-usage/blob-usage.component';

/**
 * The management page, for whoever holds `BlobManagement.Containers`: what the tenant stores - by container, by user
 * and by client - and, one container at a time, its directories and the files in them. The files are read here and
 * nothing more: a file is stored, renamed, moved and deleted by the business system that keeps it. Directories are
 * kept here, by the permissions on the container, which a user who manages grants opens from here.
 */
@Component({
  selector: 'blob-management-page',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <div class="card mb-3">
      <div class="card-body">
        <blob-usage />
      </div>
    </div>
    <div class="card">
      <div class="card-body">
        @if (loaded() && containers().length === 0) {
          <p class="text-muted mb-0">{{ 'BlobManagement::NoContainers' | abpLocalization }}</p>
        }
        @if (containers().length > 0) {
          <div class="d-flex align-items-center mb-3 gap-2">
            <ul class="nav nav-tabs flex-grow-1">
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
            @if (canManagePermissions() && selected()) {
              <button type="button" class="btn btn-sm btn-outline-secondary" (click)="permissionsOpen.set(true)">
                <i class="fa fa-user-lock me-1" aria-hidden="true"></i>{{ 'BlobManagement::Permissions' | abpLocalization }}
              </button>
            }
          </div>
        }
        @if (selected(); as container) {
          <blob-browser [container]="container" />
          @if (permissionsOpen()) {
            <abp-resource-permission-management
              [resourceName]="containerResourceName"
              [resourceKey]="container.name ?? ''"
              [resourceDisplayName]="container.name ?? ''"
              [visible]="true"
              (visibleChange)="permissionsOpen.set($event)"
            />
          }
        }
      </div>
    </div>
  `,
  imports: [CoreModule, BlobManagementBrowserComponent, BlobManagementUsageComponent, ResourcePermissionManagementComponent],
})
export class BlobManagementComponent {
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
    this.containerService.getList().subscribe(result => {
      const containers = result.items ?? [];
      this.containers.set(containers);
      this.selected.set(containers[0] ?? null);
      this.loaded.set(true);
    });
  }
}
