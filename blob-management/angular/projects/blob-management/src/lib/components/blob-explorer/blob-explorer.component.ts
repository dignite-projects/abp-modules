import { CoreModule, PermissionService } from '@abp/ng.core';
import { ResourcePermissionManagementComponent } from '@abp/ng.permission-management';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  model,
  signal,
  untracked,
} from '@angular/core';
import { EMPTY, catchError } from 'rxjs';
import { eBlobManagementPolicyNames, eBlobManagementResourceNames } from '../../blob-management-permissions';
import { BlobManagementContainerDto, BlobManagementContainerService } from '../../proxy/containers';
import { BlobManagementFileDto } from '../../proxy/files';
import { BlobManagementBrowserComponent } from '../blob-browser/blob-browser.component';
import { BlobManagementFileSelection } from '../blob-file-list/blob-file-list.component';

/**
 * The file explorer: the containers the caller has something to do in, as tabs, or only `containerName` - what a
 * business system opens for the container it keeps its files in. A user who manages grants can open a container's own
 * permissions from here. What the management page and the file picker show.
 */
@Component({
  selector: 'blob-explorer',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    @if (loaded() && containers().length === 0) {
      <p class="text-muted mb-0">{{ 'BlobManagement::NoContainers' | abpLocalization }}</p>
    }
    @if (containers().length > 1 || (canManagePermissions() && selected())) {
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
      <blob-browser [container]="container" [selection]="selection()" [(selected)]="selectedFiles" />
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
  `,
  imports: [CoreModule, BlobManagementBrowserComponent, ResourcePermissionManagementComponent],
})
export class BlobManagementExplorerComponent {
  private readonly containerService = inject(BlobManagementContainerService);
  private readonly permissionService = inject(PermissionService);

  /**
   * Only this container, read by its name - so it is shown to whoever was granted in it, a directory of it included.
   * Every container the caller has something to do in when empty.
   */
  readonly containerName = input<string | null | undefined>(null);
  readonly selection = input<BlobManagementFileSelection>('none');
  /** The files chosen, across containers, directories and pages. */
  readonly selectedFiles = model<BlobManagementFileDto[]>([]);

  readonly containers = signal<BlobManagementContainerDto[]>([]);
  readonly selected = signal<BlobManagementContainerDto | null>(null);
  readonly loaded = signal(false);
  readonly permissionsOpen = signal(false);

  readonly containerResourceName = eBlobManagementResourceNames.Container;
  readonly canManagePermissions = computed(() =>
    this.permissionService.getGrantedPolicy(eBlobManagementPolicyNames.ManagePermissions),
  );

  constructor() {
    // The container shown follows the input.
    effect(onCleanup => {
      const name = this.containerName();
      const subscription = untracked(() => this.load(name));
      onCleanup(() => subscription.unsubscribe());
    });
  }

  private load(name: string | null | undefined) {
    this.loaded.set(false);
    const done = (containers: BlobManagementContainerDto[]) => {
      this.containers.set(containers);
      this.selected.set(containers[0] ?? null);
      this.loaded.set(true);
    };

    if (name) {
      return this.containerService
        .get(name)
        .pipe(
          catchError(() => {
            done([]);
            return EMPTY;
          }),
        )
        .subscribe(container => done([container]));
    }

    return this.containerService.getList().subscribe(result => done(result.items ?? []));
  }
}
