import { TreeModule } from '@abp/ng.components/tree';
import { CoreModule } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ThemeSharedModule } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, catchError, finalize, forkJoin, map, of } from 'rxjs';
import { BlobManagementContainerDto } from '../../proxy/containers';
import { BlobManagementDirectoryDto, BlobManagementDirectoryService } from '../../proxy/directories';

/** The key of the node that stands for the top of the container. */
export const BLOB_MANAGEMENT_ROOT_KEY = '__blob-container-root__';

/** What a node stands for: a directory, or (without one) the top of the container. */
export interface BlobManagementTreeNodeEntity {
  key: string;
  directory?: BlobManagementDirectoryDto;
}

/**
 * What `abp-tree` renders: a directory, or the top of the container. `entity` is what `abp-tree` hands back when a
 * node is chosen, as ABP's own trees do.
 */
export interface BlobManagementTreeNode extends BlobManagementTreeNodeEntity {
  title: string;
  isLeaf: boolean;
  expanded: boolean;
  children: BlobManagementTreeNode[];
  entity: BlobManagementTreeNodeEntity;
}

/** The part of an `nz-tree` node this component reads. */
interface TreeNodeRef {
  key: string;
  origin: BlobManagementTreeNode;
  parentNode?: TreeNodeRef | null;
  isExpanded?: boolean;
}

/**
 * The directories of one container, loaded a level at a time: the top when the container is shown, a directory's
 * subdirectories when it is first expanded. Directories are created, renamed and deleted from a node's menu and moved
 * by dragging, each offered only when the caller may do it - which is the container's answer, the same for every
 * directory in it. Every change reloads the levels already loaded. With `manage` off it only chooses a directory - no
 * menu, no dragging.
 */
@Component({
  selector: 'blob-directory-tree',
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './blob-directory-tree.component.html',
  imports: [CoreModule, ThemeSharedModule, TreeModule, ReactiveFormsModule],
})
export class BlobManagementDirectoryTreeComponent {
  private readonly directoryService = inject(BlobManagementDirectoryService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly fb = inject(FormBuilder);

  readonly container = input.required<BlobManagementContainerDto>();
  /** Whether directories are created, renamed, deleted and moved here, or only chosen. */
  readonly manage = input(true);

  /** The directory chosen; `null` for the top of the container. */
  readonly selectionChange = output<BlobManagementDirectoryDto | null>();

  readonly rootKey = BLOB_MANAGEMENT_ROOT_KEY;
  readonly nodes = signal<BlobManagementTreeNode[]>([]);
  readonly selectedKey = signal(BLOB_MANAGEMENT_ROOT_KEY);
  expandedKeys: string[] = [BLOB_MANAGEMENT_ROOT_KEY];

  /** The subdirectories loaded so far, by the key of their parent. */
  private readonly levels = new Map<string, BlobManagementDirectoryDto[]>();
  /** Bumped when the container changes, so that answers asked for the one before are dropped. */
  private generation = 0;
  /** The latest request sent for each level, so that an older answer arriving later is dropped. */
  private readonly requests = new Map<string, number>();

  formOpen = false;
  formBusy = false;
  form: FormGroup | undefined;
  /** The directory the form renames; `undefined` when it creates one. */
  editing: BlobManagementDirectoryDto | undefined;
  /** Where the form creates a directory: a directory's key, or the root key. */
  private formParentKey = BLOB_MANAGEMENT_ROOT_KEY;

  constructor() {
    effect(() => {
      this.container();
      untracked(() => this.reset());
    });
  }

  reset(): void {
    // Whatever is still in flight belongs to the container shown before: its answers are dropped on arrival.
    this.generation++;
    this.requests.clear();
    this.levels.clear();
    this.expandedKeys = [BLOB_MANAGEMENT_ROOT_KEY];
    this.selectedKey.set(BLOB_MANAGEMENT_ROOT_KEY);
    this.load(BLOB_MANAGEMENT_ROOT_KEY).subscribe();
  }

  /**
   * Loads the subdirectories of `parentKey` and redraws the tree. An answer is dropped when the container has changed
   * since it was asked for, or when a newer request for the same level was sent after it: the newest one wins. A
   * level that can no longer be read - its directory deleted elsewhere - is forgotten, so it does not fail the
   * reloads that follow.
   */
  load(parentKey: string): Observable<void> {
    const generation = this.generation;
    const request = (this.requests.get(parentKey) ?? 0) + 1;
    this.requests.set(parentKey, request);
    const isCurrent = () => generation === this.generation && request === this.requests.get(parentKey);

    return this.directoryService
      .getList({
        containerName: this.container().name ?? '',
        parentId: parentKey === BLOB_MANAGEMENT_ROOT_KEY ? null : parentKey,
      })
      .pipe(
        map(result => {
          if (isCurrent()) {
            this.levels.set(parentKey, result.items ?? []);
            this.rebuild();
          }
        }),
        catchError(() => {
          if (isCurrent() && parentKey !== BLOB_MANAGEMENT_ROOT_KEY) {
            this.levels.delete(parentKey);
            this.expandedKeys = this.expandedKeys.filter(key => key !== parentKey);
            this.rebuild();
          }

          return of(undefined);
        }),
      );
  }

  /** Reloads every level loaded so far: a change can add, remove or move nodes anywhere among them. */
  reload(): void {
    const keys = [...this.levels.keys()];
    forkJoin(keys.map(key => this.load(key))).subscribe();
  }

  private rebuild(): void {
    const build = (directory: BlobManagementDirectoryDto): BlobManagementTreeNode => {
      const key = directory.id ?? '';
      return {
        key,
        directory,
        title: directory.name ?? '',
        isLeaf: !directory.hasChildren,
        expanded: this.expandedKeys.includes(key),
        children: (this.levels.get(key) ?? []).map(build),
        entity: { key, directory },
      };
    };

    this.nodes.set([
      {
        key: BLOB_MANAGEMENT_ROOT_KEY,
        title: this.container().name ?? '',
        isLeaf: false,
        expanded: true,
        children: (this.levels.get(BLOB_MANAGEMENT_ROOT_KEY) ?? []).map(build),
        entity: { key: BLOB_MANAGEMENT_ROOT_KEY },
      },
    ]);
  }

  /** Takes `nz-tree`'s expand event; only its node is read. */
  onExpandChange(event: { node?: unknown }): void {
    const node = event.node as TreeNodeRef | null | undefined;
    if (!node) {
      return;
    }

    if (node.isExpanded) {
      if (!this.expandedKeys.includes(node.key)) {
        this.expandedKeys = [...this.expandedKeys, node.key];
      }

      if (!this.levels.has(node.key)) {
        this.load(node.key).subscribe();
      }
    } else {
      this.expandedKeys = this.expandedKeys.filter(key => key !== node.key);
    }
  }

  /** Takes what `abp-tree` hands back for the node chosen: its `entity`. */
  select(entity: BlobManagementTreeNodeEntity | null | undefined): void {
    if (!entity || entity.key === this.selectedKey()) {
      return;
    }

    this.selectedKey.set(entity.key);
    this.selectionChange.emit(entity.directory ?? null);
  }

  isNodeSelected = (node: { key: string }): boolean => node.key === this.selectedKey();

  /** Creating directories is the container's permission, in any node: at its top or in a directory. */
  canCreate(): boolean {
    return !!this.container().access?.createDirectories;
  }

  /** Renaming and moving a directory - never the node that stands for the container. */
  canRename(node: TreeNodeRef): boolean {
    return !!node.origin.directory && !!this.container().access?.updateDirectories;
  }

  canDelete(node: TreeNodeRef): boolean {
    return !!node.origin.directory && !!this.container().access?.deleteDirectories;
  }

  openCreateForm(node: TreeNodeRef): void {
    this.editing = undefined;
    this.formParentKey = node.key;
    this.form = this.fb.group({ name: ['', [Validators.required, Validators.maxLength(256)]] });
    this.formOpen = true;
  }

  openRenameForm(node: TreeNodeRef): void {
    this.editing = node.origin.directory;
    this.form = this.fb.group({ name: [this.editing?.name ?? '', [Validators.required, Validators.maxLength(256)]] });
    this.formOpen = true;
  }

  save(): void {
    if (!this.form || this.form.invalid || this.formBusy) {
      return;
    }

    const name = (this.form.value.name as string).trim();
    const request = this.editing?.id
      ? this.directoryService.update(this.editing.id, { name })
      : this.directoryService.create({
          containerName: this.container().name ?? '',
          parentId: this.formParentKey === BLOB_MANAGEMENT_ROOT_KEY ? null : this.formParentKey,
          name,
        });

    this.formBusy = true;
    request.pipe(finalize(() => (this.formBusy = false))).subscribe(() => {
      if (!this.editing && this.formParentKey !== BLOB_MANAGEMENT_ROOT_KEY && !this.expandedKeys.includes(this.formParentKey)) {
        this.expandedKeys = [...this.expandedKeys, this.formParentKey];
        this.levels.set(this.formParentKey, []);
      }

      this.formOpen = false;
      this.reload();
    });
  }

  delete(node: TreeNodeRef): void {
    const directory = node.origin.directory;
    const id = directory?.id;
    if (!directory || !id) {
      return;
    }

    this.confirmation
      .warn('BlobManagement::DeleteDirectoryConfirmation', 'AbpUi::AreYouSure', {
        messageLocalizationParams: [directory.name ?? ''],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status !== Confirmation.Status.confirm) {
          return;
        }

        this.directoryService.delete(id).subscribe(() => {
          if (this.selectedKey() === id) {
            this.select(this.nodes()[0]);
          }

          this.levels.delete(id);
          this.reload();
        });
      });
  }

  /**
   * Moves a directory where it is dropped: into a node (first among its children), or before or after a sibling. The
   * server checks the rest - the caller's permissions, that it stays in its container and does not land inside itself.
   * The tree is redrawn from the server's answer rather than moved locally.
   */
  beforeDrop = (arg: unknown): Observable<boolean> => {
    const { pos, dragNode, node } = arg as { pos: number; dragNode: TreeNodeRef; node: TreeNodeRef };
    const moved = dragNode.origin.directory;
    if (!moved?.id || !this.canRename(dragNode)) {
      return of(false);
    }

    let parentKey: string;
    let order: number;
    if (pos === 0) {
      parentKey = node.key;
      order = 0;
    } else if (node.key === BLOB_MANAGEMENT_ROOT_KEY || !node.origin.directory) {
      return of(false);
    } else {
      parentKey = node.parentNode?.key ?? BLOB_MANAGEMENT_ROOT_KEY;
      order = (node.origin.directory.order ?? 0) + (pos === 1 ? 1 : 0);
    }

    this.directoryService
      .move(moved.id, { parentId: parentKey === BLOB_MANAGEMENT_ROOT_KEY ? null : parentKey, order })
      .subscribe({ next: () => this.reload(), error: () => this.reload() });

    return of(false);
  };
}
