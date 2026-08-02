import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CoreModule } from '@abp/ng.core';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FieldTypeControlBase } from '@dignite/ng.flex-fields';
import { FileExplorerPickerComponent } from '@dignite/ng.file-explorer';
import { FileExplorerConfiguration } from './file-explorer-configuration';

/** Edits the value of a `FileExplorer` field: pick one or more files via the file-explorer picker. */
@Component({
  selector: 'ff-file-explorer-control',
  templateUrl: './file-explorer-control.component.html',
  imports: [CommonModule, CoreModule, ReactiveFormsModule, FileExplorerPickerComponent],
})
export class FileExplorerControlComponent extends FieldTypeControlBase {
  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration['FileExplorer.UploadFileMultiple'];
  }

  get containerName(): string {
    return (this.fieldValue?.field.configuration['FileExplorer.FileContainerName'] as string) ?? '';
  }

  /** No fallback container: an unconfigured field is a misconfiguration to surface, not paper over. */
  get isContainerConfigured(): boolean {
    return this.containerName.length > 0;
  }

  protected configurationDefaults(): object {
    return new FileExplorerConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    const stored = Array.isArray(this.selectedValue) ? this.selectedValue : [];

    return this.fb.control(stored, validators);
  }

  onSelectedFileChange(files: unknown[]): void {
    this.fieldControl?.setValue(files);
  }
}
