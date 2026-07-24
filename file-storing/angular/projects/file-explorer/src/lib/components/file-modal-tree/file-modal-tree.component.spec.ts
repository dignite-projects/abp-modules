import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { FileModalTreeComponent } from './file-modal-tree.component';
import * as DirectoryDescriptorService from '../../proxy/dignite/file-explorer/directories';
import { ToasterService, ConfirmationService } from '@abp/ng.theme.shared';
import { LocalizationService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { ValidatorsService } from '../../services/validators.service';

describe('FileModalTreeComponent', () => {
  let component: FileModalTreeComponent;
  let fixture: ComponentFixture<FileModalTreeComponent>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [FileModalTreeComponent,CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [
        {
          provide: DirectoryDescriptorService.FileDescriptorService,
          useValue: { move: () => of({}) },
        },
        { provide: ToasterService, useValue: {} },
        { provide: LocalizationService, useValue: { instant: () => '' } },
        { provide: ConfirmationService, useValue: {} },
        { provide: ValidatorsService, useValue: {} },
      ],
    });
    fixture = TestBed.createComponent(FileModalTreeComponent);
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
    const firstFixture = TestBed.createComponent(FileModalTreeComponent);
    const secondFixture = TestBed.createComponent(FileModalTreeComponent);
    (firstFixture.componentInstance as any)._DescriptorService = firstService;
    (secondFixture.componentInstance as any)._DescriptorService = secondService;

    firstFixture.componentInstance.beforeDrop({
      pos: -1,
      dragNode: { key: 'first' },
      node: { key: 'node', origin: { order: 1, children: [] }, parentNode: null },
    } as any).subscribe();
    secondFixture.componentInstance.beforeDrop({
      pos: -1,
      dragNode: { key: 'second' },
      node: { key: 'node', origin: { order: 1, children: [] }, parentNode: null },
    } as any).subscribe();

    expect(firstMoves).toEqual(['first']);
    expect(secondMoves).toEqual(['second']);
  });
});
