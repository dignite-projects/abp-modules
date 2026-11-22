import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { BlobManagementFilesComponent } from './blob-management-files.component';
import { BlobManagementContainerService } from '@dignite/ng.blob-management/proxy';

describe('BlobManagementFilesComponent', () => {
  let getList: ReturnType<typeof vi.fn>;

  function container(name: string, createDirectories = false) {
    return { name, allowDirectories: true, access: { view: true, createDirectories } };
  }

  function render(...answers: object[][]) {
    getList = vi.fn();
    answers.forEach(items => getList.mockReturnValueOnce(of({ items })));
    TestBed.configureTestingModule({
      imports: [BlobManagementFilesComponent, CoreTestingModule.withConfig()],
      providers: [{ provide: BlobManagementContainerService, useValue: { getList } }],
    });
    // The page's parts have their own tests; here only what the page itself decides is run.
    TestBed.overrideComponent(BlobManagementFilesComponent, { set: { template: '', imports: [] } });
    return TestBed.createComponent(BlobManagementFilesComponent).componentInstance;
  }

  it('shows the first container', () => {
    const page = render([container('documents'), container('media')]);

    expect(page.containers().length).toBe(2);
    expect(page.selected()?.name).toBe('documents');
  });

  it('reads the containers again when the permission dialog closes, since the grants may have changed', () => {
    const page = render(
      [container('documents'), container('media')],
      [container('documents'), container('media', true)],
    );
    page.selected.set(page.containers()[1]);

    page.onPermissionsVisibleChange(true);
    expect(getList).toHaveBeenCalledTimes(1);

    page.onPermissionsVisibleChange(false);

    expect(getList).toHaveBeenCalledTimes(2);
    expect(page.permissionsOpen()).toBe(false);
    expect(page.selected()?.name).toBe('media');
    expect(page.selected()?.access?.createDirectories).toBe(true);
  });
});
