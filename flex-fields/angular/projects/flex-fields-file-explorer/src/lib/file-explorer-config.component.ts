import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '@dignite/ng.flex-fields';
import { FileExplorerConfiguration } from './file-explorer-configuration';

/** Designer-side editor for a `FileExplorer` field's configuration. */
@Component({
  selector: 'ff-file-explorer-config',
  templateUrl: './file-explorer-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class FileExplorerConfigComponent extends FieldTypeConfigBase {
  protected configurationDefaults(): object {
    return new FileExplorerConfiguration();
  }
}
