import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { BlobManagementComponent } from './blob-management.component';
import { BlobManagementContainerService } from '../proxy/containers';

describe('BlobManagementComponent', () => {
  let getList: ReturnType<typeof vi.fn>;

  function container(name: string, createDirectories = false) {
    return { name, allowDirectories: true, access: { view: true, createDirectories } };
  }

  function render(...answers: object[][]) {
    getList = vi.fn();
    answers.forEach(items => getList.mockReturnValueOnce(of({ items })));
    TestBed.configureTestingModule({
      imports: [BlobManagementComponent, CoreTestingModule.withConfig()],
      providers: [{ provide: BlobManagementContainerService, useValue: { getList } }],
    });
    // The page's parts have their own tests; here only what the page itself decides is run.
    TestBed.overrideComponent(BlobManagementComponent, { set: { template: '', imports: [] } });
    return TestBed.createComponent(BlobManagementComponent).componentInstance;
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

  it('keeps the container shown as it is when nothing about it changed, so its browser does not start over', () => {
    const page = render([container('documents')], [container('documents')]);
    const shown = page.selected();

    page.onPermissionsVisibleChange(false);

    expect(page.selected()).toBe(shown);
  });
});
