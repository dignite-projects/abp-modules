import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PermissionService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { ConfirmationService } from '@abp/ng.theme.shared';
import { BlobManagementFileMoveModalComponent } from './blob-file-move-modal.component';
import { BlobManagementDirectoryService } from '../../proxy/directories';
import { BlobManagementFileService } from '../../proxy/files';

describe('BlobManagementFileMoveModalComponent', () => {
  let move: ReturnType<typeof vi.fn>;

  function render(access: object = { view: true, uploadFiles: true }) {
    move = vi.fn(() => of({ id: 'f1', directoryId: 'd1' }));
    TestBed.configureTestingModule({
      imports: [BlobManagementFileMoveModalComponent, CoreTestingModule.withConfig()],
      providers: [
        { provide: BlobManagementFileService, useValue: { move } },
        { provide: BlobManagementDirectoryService, useValue: { getList: () => of({ items: [] }) } },
        { provide: ConfirmationService, useValue: {} },
        { provide: PermissionService, useValue: { getGrantedPolicy: () => false, getGrantedPolicy$: () => of(false) } },
      ],
    });
    const fixture = TestBed.createComponent(BlobManagementFileMoveModalComponent);
    fixture.componentRef.setInput('container', { name: 'documents', allowDirectories: true, access });
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('moves only somewhere else than where the file is, where the user may upload', () => {
    const modal = render();
    modal.file.set({ id: 'f1', directoryId: 'd1' });

    modal.target.set({ id: 'd1', access: { view: true, uploadFiles: true } });
    expect(modal.canMove()).toBe(false);

    modal.target.set({ id: 'd2', access: { view: true, uploadFiles: false } });
    expect(modal.canMove()).toBe(false);

    modal.target.set({ id: 'd2', access: { view: true, uploadFiles: true } });
    expect(modal.canMove()).toBe(true);

    // The top of the container is the container's own answer.
    modal.target.set(null);
    expect(modal.canMove()).toBe(true);
  });

  it('does not offer the top of a container the user may not upload to', () => {
    const modal = render({ view: true });
    modal.file.set({ id: 'f1', directoryId: 'd1' });

    expect(modal.canMove()).toBe(false);
  });

  it('moves the file, hands it over and closes', () => {
    const modal = render();
    const moved: unknown[] = [];
    modal.moved.subscribe(file => moved.push(file));
    modal.file.set({ id: 'f1', directoryId: null });
    modal.target.set({ id: 'd1', access: { view: true, uploadFiles: true } });

    modal.move();

    expect(move).toHaveBeenCalledWith('f1', { directoryId: 'd1' });
    expect(moved).toEqual([{ id: 'f1', directoryId: 'd1' }]);
    expect(modal.file()).toBeNull();
  });
});
