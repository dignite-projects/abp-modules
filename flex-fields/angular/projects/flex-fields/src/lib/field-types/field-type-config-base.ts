import { Directive, Input, inject } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { FlexFieldData } from '../models';

/**
 * Shared plumbing for every field type's **config** component — the designer-side editor that writes
 * a field's configuration.
 *
 * It swaps a `configuration` group into the field-definition form whose shape is this field type's,
 * then patches in the already-stored values when the field being edited is of this type.
 */
@Directive()
export abstract class FieldTypeConfigBase {
  protected readonly fb = inject(FormBuilder);

  /** Which field type this editor configures — the registration key, e.g. `Text`. */
  protected fieldTypeName?: string;
  @Input()
  set type(value: string) {
    this.fieldTypeName = value;
    this.rebuild();
  }

  /** The field-definition form being edited. */
  protected formEntity?: FormGroup;
  @Input()
  set Entity(value: FormGroup) {
    this.formEntity = value;
    this.rebuild();
  }

  /** The field definition being edited, if an existing one was selected. */
  protected selectedField?: FlexFieldData;
  @Input()
  set selected(value: FlexFieldData) {
    this.selectedField = value;
    this.rebuild();
  }

  get configuration(): FormGroup {
    return this.formEntity?.get('configuration') as FormGroup;
  }

  /** Seed values for this field type's configuration keys, as a `FormBuilder.group()` shape. */
  protected abstract configurationDefaults(): object;

  /**
   * Runs after stored configuration has been patched into the group. Override when a field type has
   * to derive editor state from what was loaded — the date editor re-formats its bounds to the
   * loaded input mode, for instance.
   */
  protected onConfigurationPatched(): void {
    // Nothing by default.
  }

  /**
   * Runs instead of {@link onConfigurationPatched} when there is nothing stored to load — a field
   * being created, or one whose type was just switched to this one.
   */
  protected onConfigurationReset(): void {
    // Nothing by default.
  }

  private rebuild(): void {
    if (!this.formEntity || !this.fieldTypeName) {
      return;
    }

    this.formEntity.setControl('configuration', this.fb.group(this.configurationDefaults()));

    // Only when the selected field is actually of this type: the designer keeps every type's config
    // editor around and swaps which one is shown, so a stale selection of a different type would
    // otherwise patch its keys into this one's group.
    if (this.selectedField?.fieldTypeName === this.fieldTypeName) {
      this.configuration.patchValue({ ...this.selectedField.configuration });
      this.onConfigurationPatched();
    } else {
      this.onConfigurationReset();
    }
  }
}
