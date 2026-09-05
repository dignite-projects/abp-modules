import { AbstractControl } from '@angular/forms';
import { LocalizationService } from '@abp/ng.core';

/**
 * Maps a mounted flex field's own control errors to the same localized text, regardless of which
 * field type produced them or how deep the control is nested (top-level, a Table column, a Matrix
 * sub-field): every built-in field type only ever raises `required`/`min`/`max`/`maxlength`, since
 * that's the full set `NumberControlComponent`/`TextControlComponent`/etc. push.
 * Gated on `touched` so a fresh, still-empty required field doesn't show red before the user reaches it.
 */
export function flexFieldErrorMessage(
  control: AbstractControl | null | undefined,
  localization: LocalizationService,
): string | null {
  const errors = control?.touched ? control.errors : null;
  if (!errors) {
    return null;
  }

  if (errors['required']) {
    return localization.instant('AbpValidation::ThisFieldIsRequired');
  }
  if (errors['min']) {
    return localization.instant('FlexFields::Validate:MinValue', String(errors['min'].min));
  }
  if (errors['max']) {
    return localization.instant('FlexFields::Validate:MaxValue', String(errors['max'].max));
  }
  if (errors['maxlength']) {
    return localization.instant(
      'FlexFields::Validate:MaxLength',
      String(errors['maxlength'].requiredLength),
    );
  }
  return null;
}
