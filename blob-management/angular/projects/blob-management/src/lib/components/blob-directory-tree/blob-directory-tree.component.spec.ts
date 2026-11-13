import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { BLOB_MANAGEMENT_ROOT_KEY, BlobManagementDirectoryTreeComponent } from './blob-directory-tree.component';
import { BlobManagementDirectoryService } from '../../proxy/directories';

describe('BlobManagementDirectoryTreeComponent', () => {
  let getList: ReturnType<typeof vi.fn>;
  let move: ReturnType<typeof vi.fn>;
  let fixture: ComponentFixture<BlobManagementDirectoryTreeComponent>;

  const docs = { id: 'd1', name: 'Docs', order: 1, hasChildren: true };
  const locked = { id: 'd2', name: 'Locked', order: 2 };
  const all = { view: true, createDirectories: true, updateDirectories: true, deleteDirectories: true };

  function node(directory?: object, key = BLOB_MANAGEMENT_ROOT_KEY, parentNode: object | null = null) {
    return { key, origin: { key, directory }, parentNode, isExpanded: true } as never;
  }

  function render(levels: Record<string, unknown[]> = { root: [docs, locked] }, access: object = all) {
    getList = vi.fn((input: { parentId?: string | null }) => of({ items: levels[input.parentId ?? 'root'] ?? [] }));
    move = vi.fn(() => of({}));
    TestBed.configureTestingModule({
      imports: [BlobManagementDirectoryTreeComponent, CoreTestingModule.withConfig()],
      providers: [
        { provide: BlobManagementDirectoryService, useValue: { getList, move } },
        { provide: ConfirmationService, useValue: {} },
      ],
    });
    fixture = TestBed.createComponent(BlobManagementDirectoryTreeComponent);
    fixture.componentRef.setInput('container', { name: 'documents', allowDirectories: true, access });
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('loads the top of the container, under a node that stands for it', () => {
    const tree = render();

    expect(getList).toHaveBeenCalledWith({ containerName: 'documents', parentId: null });
    expect(tree.nodes()[0].key).toBe(BLOB_MANAGEMENT_ROOT_KEY);
    expect(tree.nodes()[0].children.map(child => child.title)).toEqual(['Docs', 'Locked']);
    expect(tree.nodes()[0].children[0].isLeaf).toBe(false);
  });

  it('offers what the container allows, the same for every directory, and never renames or deletes its top', () => {
    const tree = render(undefined, { view: true, createDirectories: true, updateDirectories: true, deleteDirectories: false });

    expect(tree.canCreate()).toBe(true);
    expect(tree.canRename(node(docs, 'd1'))).toBe(true);
    expect(tree.canRename(node(locked, 'd2'))).toBe(true);
    expect(tree.canDelete(node(docs, 'd1'))).toBe(false);
    expect(tree.canRename(node())).toBe(false);
    expect(tree.canDelete(node())).toBe(false);

    TestBed.resetTestingModule();
    expect(render(undefined, { view: true }).canCreate()).toBe(false);
  });

  it('loads a level when it is first expanded, and not again', () => {
    const tree = render({ root: [docs], d1: [{ id: 'd3', name: 'Below' }] });

    tree.onExpandChange({ node: node(docs, 'd1') });
    tree.onExpandChange({ node: node(docs, 'd1') });

    expect(getList).toHaveBeenCalledTimes(2);
    expect(tree.nodes()[0].children[0].children.map(child => child.title)).toEqual(['Below']);
  });

  it('forgets a level that can no longer be read, so the reloads that follow go on', () => {
    const tree = render({ root: [docs], d1: [] });
    tree.onExpandChange({ node: node(docs, 'd1') });

    // The directory was deleted elsewhere: its level answers with an error from now on.
    getList.mockImplementation((input: { parentId?: string | null }) =>
      input.parentId === 'd1' ? throwError(() => new Error('not found')) : of({ items: [locked] }),
    );
    tree.reload();

    expect(tree.nodes()[0].children.map(child => child.title)).toEqual(['Locked']);
    expect(tree.expandedKeys).not.toContain('d1');

    getList.mockClear();
    tree.reload();
    expect(getList).toHaveBeenCalledTimes(1);
    expect(getList).toHaveBeenCalledWith({ containerName: 'documents', parentId: null });
  });

  it('chooses a directory, or the top of the container', () => {
    const tree = render();
    const chosen: unknown[] = [];
    tree.selectionChange.subscribe(directory => chosen.push(directory));

    tree.select({ key: 'd1', directory: docs });
    tree.select({ key: 'd1', directory: docs });
    tree.select({ key: BLOB_MANAGEMENT_ROOT_KEY });

    expect(chosen).toEqual([docs, null]);
  });

  it('moves a directory where it is dropped, when the caller may move directories', () => {
    render(undefined, { view: true }).beforeDrop({ pos: 0, dragNode: node(locked, 'd2'), node: node(docs, 'd1') });
    expect(move).not.toHaveBeenCalled();

    TestBed.resetTestingModule();
    const tree = render();

    tree.beforeDrop({ pos: 0, dragNode: node(docs, 'd1'), node: node(locked, 'd2') });
    expect(move).toHaveBeenLastCalledWith('d1', { parentId: 'd2', order: 0 });

    tree.beforeDrop({ pos: 1, dragNode: node(docs, 'd1'), node: node(locked, 'd2', { key: BLOB_MANAGEMENT_ROOT_KEY }) });
    expect(move).toHaveBeenLastCalledWith('d1', { parentId: null, order: 3 });
  });
});
