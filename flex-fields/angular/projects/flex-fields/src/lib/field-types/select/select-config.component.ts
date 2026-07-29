import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FieldTypeConfigBase } from '../field-type-config-base';
import { SelectConfiguration } from './select-configuration';
import { normalizeSelectListItems } from './select-list-item';

let nextCheckboxId = 0;

/** Designer-side editor for a `Select` field's configuration: its option list and how it renders. */
@Component({
  selector: 'ff-select-config',
  templateUrl: './select-config.component.html',
  imports: [CoreModule, ReactiveFormsModule, DragDropModule],
})
export class SelectConfigComponent extends FieldTypeConfigBase {
  readonly multipleId = `ff-select-multiple-${nextCheckboxId++}`;

  get options(): FormArray {
    return this.configuration.controls['Select.Options'] as FormArray;
  }

  protected configurationDefaults(): object {
    return new SelectConfiguration();
  }

  protected override onConfigurationPatched(): void {
    // patchValue cannot grow a FormArray, so one row per stored option has to exist first.
    // Normalizing here is what lets the rows bind to `Text`/`Value`/`Selected` regardless of the
    // casing the options came back in.
    const stored = normalizeSelectListItems(this.selectedField?.configuration['Select.Options']);
    stored.forEach(() => this.addOption());
    this.configuration.patchValue({ 'Select.Options': stored });
  }

  protected override onConfigurationReset(): void {
    // A field being created starts with one blank row rather than an empty table.
    this.addOption();
  }

  addOption(): void {
    this.options.push(
      new FormGroup({
        Text: new FormControl('', Validators.required),
        Value: new FormControl('', Validators.required),
        Selected: new FormControl(false),
      }),
    );
  }

  deleteOption(index: number): void {
    this.options.removeAt(index);
  }

  /** Seeds `Value` from `Text` for a new option, but never overwrites one the user has typed. */
  onTextChange(event: Event, index: number): void {
    const option = this.options.at(index);

    if (option.get('Value')?.value) {
      return;
    }

    option.patchValue({ Value: (event.target as HTMLInputElement).value });
  }

  drop(event: CdkDragDrop<unknown>): void {
    moveItemInArray(this.options.controls, event.previousIndex, event.currentIndex);
    this.options.updateValueAndValidity();
  }
}
