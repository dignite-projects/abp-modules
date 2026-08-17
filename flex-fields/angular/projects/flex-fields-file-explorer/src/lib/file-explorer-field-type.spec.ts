import { TestBed } from '@angular/core/testing';
import { FieldTypeResolver } from '@dignite/ng.flex-fields';
import { FILE_EXPLORER_FIELD_TYPE } from './file-explorer-field-type';
import { FileExplorerConfigComponent } from './file-explorer-config.component';
import { FileExplorerControlComponent } from './file-explorer-control.component';
import { FileExplorerViewComponent } from './file-explorer-view.component';
import { provideFileExplorerFieldType } from './provide-file-explorer-field-type';

describe('FILE_EXPLORER_FIELD_TYPE', () => {
  it('registers under the stable "FileExplorer" key the server expects', () => {
    expect(FILE_EXPLORER_FIELD_TYPE.name).toBe('FileExplorer');
  });

  it('wires config/control/view, and deliberately ships no search component', () => {
    expect(FILE_EXPLORER_FIELD_TYPE.configComponent).toBe(FileExplorerConfigComponent);
    expect(FILE_EXPLORER_FIELD_TYPE.controlComponent).toBe(FileExplorerControlComponent);
    expect(FILE_EXPLORER_FIELD_TYPE.viewComponent).toBe(FileExplorerViewComponent);
    expect(FILE_EXPLORER_FIELD_TYPE.searchComponent).toBeUndefined();
  });
});

describe('provideFileExplorerFieldType', () => {
  it('registers the FileExplorer field type through the standard FLEX_FIELD_TYPES multi-provider', () => {
    TestBed.configureTestingModule({ providers: [provideFileExplorerFieldType()] });

    const resolver = TestBed.inject(FieldTypeResolver);

    expect(resolver.get('FileExplorer')).toBe(FILE_EXPLORER_FIELD_TYPE);
  });
});
