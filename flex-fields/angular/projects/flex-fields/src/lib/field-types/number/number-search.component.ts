import { Component } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { FieldTypeControlBase } from '../field-type-control-base';
import { NumberConfiguration } from './number-configuration';

/**
 * Filters by a `Number` field, as a min–max range.
 *
 * The two visible inputs live in their own form; what actually reaches the query is the single
 * `"min-max"` string written into the field's control, and it is only written once both ends are set.
 */
@Component({
  selector: 'ff-number-search',
  templateUrl: './number-search.component.html',
  imports: [CommonModule, ReactiveFormsModule],
})
export class NumberSearchComponent extends FieldTypeControlBase {
  readonly numberForm = new FormGroup({
    min: new FormControl<number | null>(null),
    max: new FormControl<number | null>(null),
  });

  constructor() {
    super();
    this.numberForm.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.applyRange());
  }

  protected configurationDefaults(): object {
    return new NumberConfiguration();
  }

  protected createControl(): AbstractControl {
    return this.fb.control(this.selectedValue);
  }

  private applyRange(): void {
    if (!this.fieldValue) {
      return;
    }

    const configuration = this.fieldValue.field.configuration;
    const configuredMin = this.toNumber(configuration['Number.Min']);
    const configuredMax = this.toNumber(configuration['Number.Max']);

    let min = this.numberForm.value.min ?? null;
    let max = this.numberForm.value.max ?? null;

    // Clamp to the configured bounds — but only where a bound is actually configured. The old library
    // ran the comparison against Number('') === 0, so an unbounded field silently rejected negatives.
    if (max !== null) {
      if (configuredMin !== null && max < configuredMin) max = configuredMin;
      if (configuredMax !== null && max > configuredMax) max = configuredMax;
    }

    if (min !== null) {
      if (configuredMin !== null && min < configuredMin) min = configuredMin;
      if (configuredMax !== null && min > configuredMax) min = configuredMax;
      if (max !== null && min > max) min = max;
    }

    // emitEvent: false, or writing the clamped values back would re-enter this handler.
    if (min !== this.numberForm.value.min || max !== this.numberForm.value.max) {
      this.numberForm.patchValue({ min, max }, { emitEvent: false });
    }

    // The clamped values, not the raw ones the old library sent — otherwise the inputs showed one
    // range and the query used another.
    this.fieldControl?.patchValue(min !== null && max !== null ? `${min}-${max}` : '');
  }

  private toNumber(value: unknown): number | null {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const parsed = Number(value);
    return Number.isNaN(parsed) ? null : parsed;
  }
}
