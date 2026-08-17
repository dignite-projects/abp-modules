import { TestBed } from '@angular/core/testing';
import { EnvironmentService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { FileExplorerViewComponent } from './file-explorer-view.component';

describe('FileExplorerViewComponent', () => {
  beforeEach(() => {
    // fe-file-preview (rendered whenever there's at least one file) injects FileDescriptorService ->
    // RestService, which needs CORE_OPTIONS, and on ngOnChanges calls getStream() -> EnvironmentService
    // .getApiUrl('FileExplorer') - withConfig() alone leaves `apis` empty, so seed it directly.
    TestBed.configureTestingModule({ imports: [CoreTestingModule.withConfig()] });
    TestBed.inject(EnvironmentService).setState({
      apis: { default: { url: 'https://api.example.com' }, FileExplorer: { url: 'https://api.example.com' } },
    } as unknown as Parameters<EnvironmentService['setState']>[0]);
  });

  function render(value: unknown, showInList = false) {
    const fixture = TestBed.createComponent(FileExplorerViewComponent);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('showInList', showInList);
    fixture.detectChanges();
    return fixture;
  }

  it('treats a non-array value as no files, rather than throwing', () => {
    expect(render(undefined).componentInstance.files).toEqual([]);
    expect(render(null).componentInstance.files).toEqual([]);
    expect(render('not-an-array').componentInstance.files).toEqual([]);
  });

  it('passes an array value straight through', () => {
    const files = [{ url: 'a.png', name: 'a.png' }, { url: 'b.png', name: 'b.png' }];

    expect(render(files).componentInstance.files).toEqual(files);
  });

  it('shows a dash placeholder when there are no files', () => {
    const listMode = render([], true);
    expect(listMode.nativeElement.textContent).toContain('—');

    const detailMode = render([], false);
    expect(detailMode.nativeElement.textContent).toContain('—');
  });

  it('previews the first file and counts the rest', () => {
    const files = [
      { url: 'a.png', name: 'a.png', containerName: 'attachments', blobName: 'a', mimeType: 'image/png' },
      { url: 'b.png', name: 'b.png' },
      { url: 'c.png', name: 'c.png' },
    ];

    const fixture = render(files);

    expect(fixture.nativeElement.querySelector('fe-file-preview')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('+2');
  });

  it('shows no counter badge for a single file', () => {
    const fixture = render([{ url: 'a.png', name: 'a.png' }]);

    expect(fixture.nativeElement.textContent).not.toContain('+');
  });
});
