import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { FileExplorerDirectoryTreeComponent, MY_FILES_NODE_KEY } from './file-explorer-directory-tree.component';
import * as DirectoryDescriptorService from '../../proxy/dignite/file-explorer/directories';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { LocalizationService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { ValidatorsService } from '../../services/validators.service';

describe('FileExplorerDirectoryTreeComponent', () => {
  let component: FileExplorerDirectoryTreeComponent;
  let fixture: ComponentFixture<FileExplorerDirectoryTreeComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileExplorerDirectoryTreeComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [
        {
          provide: DirectoryDescriptorService.DirectoryDescriptorService,
          useValue: { move: () => of({}) },
        },
        { provide: LocalizationService, useValue: { instant: () => '' } },
        { provide: ConfirmationService, useValue: {} },
        { provide: ValidatorsService, useValue: {} },
      ],
    });
    fixture = TestBed.createComponent(FileExplorerDirectoryTreeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('keeps directory move requests isolated between component instances', () => {
    const firstMoves: string[] = [];
    const secondMoves: string[] = [];
    const firstService = {
      move: (id: string) => {
        firstMoves.push(id);
        return of({});
      },
      getList: () => of({ items: [] }),
    };
    const secondService = {
      move: (id: string) => {
        secondMoves.push(id);
        return of({});
      },
      getList: () => of({ items: [] }),
    };
    const firstFixture = TestBed.createComponent(FileExplorerDirectoryTreeComponent);
    const secondFixture = TestBed.createComponent(FileExplorerDirectoryTreeComponent);
    (firstFixture.componentInstance as any).directoryDescriptorService = firstService;
    (secondFixture.componentInstance as any).directoryDescriptorService = secondService;

    firstFixture.componentInstance.beforeDrop({
      pos: 0,
      dragNode: { key: 'first' },
      node: { key: MY_FILES_NODE_KEY, origin: { order: 1, children: [] }, parentNode: null },
    } as any).subscribe();
    secondFixture.componentInstance.beforeDrop({
      pos: 0,
      dragNode: { key: 'second' },
      node: { key: MY_FILES_NODE_KEY, origin: { order: 1, children: [] }, parentNode: null },
    } as any).subscribe();

    expect(firstMoves).toEqual(['first']);
    expect(secondMoves).toEqual(['second']);
  });

  it('keeps the component context when the tree invokes beforeDrop as a callback', () => {
    const moves: Array<{ id: string; input: unknown }> = [];
    const service = {
      move: (id: string, input: unknown) => {
        moves.push({ id, input });
        return of({});
      },
      getList: () => of({ items: [] }),
    };
    (component as any).directoryDescriptorService = service;

    const beforeDrop = component.beforeDrop;
    beforeDrop({
      pos: 0,
      dragNode: { key: 'dragged' },
      node: { key: MY_FILES_NODE_KEY, origin: { order: 2, children: [] }, parentNode: null },
    } as any).subscribe();

    expect(moves).toEqual([{ id: 'dragged', input: { parentId: null, order: 1 } }]);
  });

  it('moves a directory into another directory', () => {
    const moves: Array<{ id: string; input: unknown }> = [];
    (component as any).directoryDescriptorService = {
      move: (id: string, input: unknown) => {
        moves.push({ id, input });
        return of({});
      },
      getList: () => of({ items: [] }),
    };

    component.beforeDrop({
      pos: 0,
      dragNode: { key: 'dragged' },
      node: { key: 'target-directory', origin: { order: 1, children: [] }, parentNode: null },
    } as any).subscribe(result => expect(result).toBe(true));

    expect(moves).toEqual([{ id: 'dragged', input: { parentId: 'target-directory', order: 1 } }]);
  });

  it('supports same-level directory reordering', () => {
    const moves: Array<{ id: string; input: unknown }> = [];
    (component as any).directoryDescriptorService = {
      move: (id: string, input: unknown) => {
        moves.push({ id, input });
        return of({});
      },
      getList: () => of({ items: [] }),
    };

    component.beforeDrop({
      pos: 1,
      dragNode: { key: 'dragged' },
      node: {
        key: 'sibling',
        origin: { order: 1, children: [] },
        parentNode: { key: MY_FILES_NODE_KEY },
      },
    } as any).subscribe(result => expect(result).toBe(true));

    expect(moves).toEqual([{ id: 'dragged', input: { parentId: null, order: 2 } }]);
  });

  it('treats the indented gap before a first child as a sibling drop after its parent', () => {
    const moves: Array<{ id: string; input: unknown }> = [];
    (component as any).directoryDescriptorService = {
      move: (id: string, input: unknown) => {
        moves.push({ id, input });
        return of({});
      },
      getList: () => of({ items: [] }),
    };
    const parentNode: any = {
      key: 'target-directory',
      origin: { order: 3, children: [] },
      parentNode: { key: MY_FILES_NODE_KEY },
      children: [],
    };
    const firstChild: any = {
      key: 'first-child',
      origin: { order: 1, children: [] },
      parentNode,
    };
    parentNode.children = [firstChild];

    component.beforeDrop({
      pos: -1,
      dragNode: { key: 'dragged' },
      node: firstChild,
    } as any).subscribe(result => expect(result).toBe(false));

    expect(moves).toEqual([{ id: 'dragged', input: { parentId: null, order: 4 } }]);
  });
});
