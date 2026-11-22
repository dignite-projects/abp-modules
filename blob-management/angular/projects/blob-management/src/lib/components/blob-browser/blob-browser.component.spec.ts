import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { BlobManagementBrowserComponent } from './blob-browser.component';

describe('BlobManagementBrowserComponent', () => {
  let fixture: ComponentFixture<BlobManagementBrowserComponent>;

  function render() {
    TestBed.configureTestingModule({
      imports: [BlobManagementBrowserComponent, CoreTestingModule.withConfig()],
    });
    // The tree and the file list have their own tests; here only what the browser itself keeps is run.
    TestBed.overrideComponent(BlobManagementBrowserComponent, { set: { template: '', imports: [] } });
    fixture = TestBed.createComponent(BlobManagementBrowserComponent);
    fixture.componentRef.setInput('container', { name: 'documents', allowDirectories: true, access: { view: true } });
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('keeps the directory chosen when only what the caller may do with the container changes', () => {
    const browser = render();
    browser.directory.set({ id: 'd1', name: 'Docs' });

    fixture.componentRef.setInput('container', {
      name: 'documents',
      allowDirectories: true,
      access: { view: true, createDirectories: true },
    });
    fixture.detectChanges();

    expect(browser.directory()?.id).toBe('d1');
  });

  it('forgets the directory chosen when another container is shown', () => {
    const browser = render();
    browser.directory.set({ id: 'd1', name: 'Docs' });

    fixture.componentRef.setInput('container', { name: 'media', allowDirectories: true, access: { view: true } });
    fixture.detectChanges();

    expect(browser.directory()).toBeNull();
  });
});
