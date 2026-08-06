import { Component, Input, OnDestroy, OnInit, ViewEncapsulation, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CoreModule, RestService } from '@abp/ng.core';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { CKEditorModule } from '@ckeditor/ckeditor5-angular';
import type { EditorRelaxedConstructor } from '@ckeditor/ckeditor5-integrations-common';
import type { Editor, EditorConfig } from 'ckeditor5';
import { FieldTypeControlBase } from '@dignite/ng.flex-fields';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorMode } from './ckeditor-mode';
import { buildEditorConfig, resolveEditorClass } from './ckeditor-editor-config';
import { CKEditorConfiguration } from './ckeditor-configuration';
import { CKEditorUploadAdapter } from './ckeditor-upload-adapter';

/**
 * Edits the value of a `CKEditor` field.
 *
 * The editor class (Mode), plugin list (ContentFormat's Markdown swap), and image-upload wiring
 * (ImagesContainerName) are all decided once, at editor-creation time, from this field's own
 * configuration - see `ckeditor-editor-config.ts` for why none of the three is runtime-togglable
 * afterwards. `ngOnInit` doing this exactly once (rather than reacting to `fields` re-assignment) is
 * safe because the host dispatcher always creates a fresh component instance for a changed field
 * rather than reusing an existing one.
 */
@Component({
  selector: 'ff-ckeditor-control',
  templateUrl: './ckeditor-control.component.html',
  styleUrl: './ckeditor-control.component.css',
  // CKEditor 5 attaches its balloons/dropdowns to elements outside this component's own template
  // subtree (typically appended near document.body), which Angular's default per-component style
  // scoping (view encapsulation) would not reach - the imported ckeditor5.css must apply globally
  // for the editor to render with any layout at all. See ckeditor-control.component.css.
  encapsulation: ViewEncapsulation.None,
  imports: [CommonModule, CoreModule, ReactiveFormsModule, CKEditorModule],
})
export class CKEditorControlComponent extends FieldTypeControlBase implements OnInit, OnDestroy {
  private readonly restService = inject(RestService);

  /**
   * Whether this usage ever had a real stored value - captured here because
   * `FieldTypeControlBase`'s own `selected` setter collapses `null`/`undefined` down to `''`
   * (`this.selectedValue = value ?? ''`), so by the time `createControl()` runs, `selectedValue` alone
   * can no longer distinguish "never set" from "explicitly saved as an empty string". That
   * distinction matters here: InitialContent should only seed a field that has never had a value,
   * never overwrite one a user genuinely cleared and saved.
   */
  private hasStoredValue = false;

  @Input()
  override set selected(value: unknown) {
    this.hasStoredValue = value !== null && value !== undefined;
    super.selected = value;
  }

  editorClass: EditorRelaxedConstructor | null = null;
  editorConfig: EditorConfig | null = null;

  protected configurationDefaults(): object {
    return new CKEditorConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];
    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    const configuration = this.fieldValue!.field.configuration;
    const initialContent = (configuration['CKEditor.InitialContent'] as string) ?? '';
    const seeded = this.hasStoredValue ? String(this.selectedValue) : initialContent;

    // CKEditorComponent (from @ckeditor/ckeditor5-angular) implements ControlValueAccessor itself
    // (unlike FileExplorerPickerComponent), so binding [formControlName] directly on <ckeditor> in the
    // template is enough - no manual (change) => setValue() plumbing needed.
    return this.fb.control(seeded, validators);
  }

  async ngOnInit(): Promise<void> {
    const module = await import('ckeditor5');
    const configuration = this.fieldValue!.field.configuration;

    const mode = Number(configuration['CKEditor.Mode'] ?? CKEditorMode.Full) as CKEditorMode;
    const contentFormat = Number(
      configuration['CKEditor.ContentFormat'] ?? CKEditorContentFormat.Html,
    ) as CKEditorContentFormat;
    const containerName = (configuration['CKEditor.ImagesContainerName'] as string) ?? '';

    this.editorClass = resolveEditorClass(module, mode);
    this.editorConfig = buildEditorConfig(module, {
      mode,
      contentFormat,
      imageUploadEnabled: containerName.length > 0,
    });
  }

  onReady(editor: Editor): void {
    const containerName = (this.fieldValue!.field.configuration['CKEditor.ImagesContainerName'] as string) ?? '';
    if (!containerName) {
      return;
    }

    editor.plugins.get('FileRepository').createUploadAdapter = loader =>
      new CKEditorUploadAdapter(loader, containerName, this.restService);
  }
}
