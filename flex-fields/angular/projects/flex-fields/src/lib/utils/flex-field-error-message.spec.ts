import { LocalizationService } from '@abp/ng.core';
import { FormControl, Validators } from '@angular/forms';
import { flexFieldErrorMessage } from './flex-field-error-message';

/**
 * A stub rather than the real service: what matters here is *which key* and *which bound* the mapping
 * reaches for — both are wire values shared with the server's `FlexFields` localization resource —
 * not what the resource happens to render them as.
 */
const localization = {
  instant: (key: string, ...args: string[]) => [key, ...args].join('|'),
} as unknown as LocalizationService;

describe('flexFieldErrorMessage', () => {
  it('says nothing about a control the user has not reached yet', () => {
    const control = new FormControl('', Validators.required);
    expect(control.errors).toEqual({ required: true });
    expect(flexFieldErrorMessage(control, localization)).toBeNull();
  });

  it('says nothing when there is no control at all', () => {
    expect(flexFieldErrorMessage(null, localization)).toBeNull();
    expect(flexFieldErrorMessage(undefined, localization)).toBeNull();
  });

  it('says nothing about a touched, valid control', () => {
    const control = new FormControl('something', Validators.required);
    control.markAsTouched();
    expect(flexFieldErrorMessage(control, localization)).toBeNull();
  });

  it('maps required to the shared ABP key, not one of its own', () => {
    const control = new FormControl('', Validators.required);
    control.markAsTouched();
    expect(flexFieldErrorMessage(control, localization)).toBe('AbpValidation::ThisFieldIsRequired');
  });

  it('maps min to FlexFields::Validate:MinValue, bound included', () => {
    const control = new FormControl(1, Validators.min(5));
    control.markAsTouched();
    expect(flexFieldErrorMessage(control, localization)).toBe('FlexFields::Validate:MinValue|5');
  });

  it('maps max to FlexFields::Validate:MaxValue, bound included', () => {
    const control = new FormControl(9, Validators.max(5));
    control.markAsTouched();
    expect(flexFieldErrorMessage(control, localization)).toBe('FlexFields::Validate:MaxValue|5');
  });

  it('maps maxlength to FlexFields::Validate:MaxLength, required length included', () => {
    const control = new FormControl('abcdef', Validators.maxLength(3));
    control.markAsTouched();
    expect(flexFieldErrorMessage(control, localization)).toBe('FlexFields::Validate:MaxLength|3');
  });

  it('reports required first when a control carries more than one error', () => {
    const control = new FormControl(1);
    control.setErrors({ required: true, min: { min: 5, actual: 1 } });
    control.markAsTouched();
    expect(flexFieldErrorMessage(control, localization)).toBe('AbpValidation::ThisFieldIsRequired');
  });
});
