import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '@dignite/ng.flex-fields';
import { CKEditorConfiguration } from './ckeditor-configuration';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorMode } from './ckeditor-mode';

/** Designer-side editor for a `CKEditor` field's configuration. */
@Component({
  selector: 'ff-ckeditor-config',
  templateUrl: './ckeditor-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class CKEditorConfigComponent extends FieldTypeConfigBase {
  protected readonly CKEditorMode = CKEditorMode;
  protected readonly CKEditorContentFormat = CKEditorContentFormat;

  /** Basic mode's toolbar has no image support (see `buildEditorConfig`) - mirrors the selected Mode. */
  protected showImagesContainerName = true;

  protected configurationDefaults(): object {
    return new CKEditorConfiguration();
  }

  protected override onConfigurationPatched(): void {
    this.syncImagesContainerVisibility();
  }

  /**
   * Re-syncs visibility on every Mode change, and - only for this, an actual user-driven change, not
   * the initial load in `onConfigurationPatched` - clears a stored container name that no longer
   * applies, the same "re-derive dependent state on mode change" idiom `DateTimeConfigComponent` uses
   * for its Min/Max bounds. Leaving initial load non-mutating means opening an existing field and
   * cancelling out never changes its stored configuration.
   */
  protected onModeChange(): void {
    this.syncImagesContainerVisibility();

    if (!this.showImagesContainerName) {
      this.configuration.patchValue({ 'CKEditor.ImagesContainerName': '' });
    }
  }

  private syncImagesContainerVisibility(): void {
    const mode = this.configuration.value['CKEditor.Mode'] as CKEditorMode;
    this.showImagesContainerName = mode === CKEditorMode.Full;
  }
}
