import { TestBed } from '@angular/core/testing';
import { FormArray, FormControl, FormGroup, Validators } from '@angular/forms';
import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ValidatorsService } from './validators.service';

describe('ValidatorsService', () => {
  let toaster: { warn: ReturnType<typeof vi.fn> };
  let localization: { instant: ReturnType<typeof vi.fn> };
  let service: ValidatorsService;

  beforeEach(() => {
    toaster = { warn: vi.fn() };
    localization = { instant: vi.fn((key: string) => key) };
    TestBed.configureTestingModule({
      providers: [
        { provide: ToasterService, useValue: toaster },
        { provide: LocalizationService, useValue: localization },
      ],
    });
    service = TestBed.inject(ValidatorsService);
  });

  describe('isCheckForm', () => {
    it('returns false and warns nothing when every field is valid', () => {
      expect(service.isCheckForm({ name: true, email: true })).toBe(false);
      expect(toaster.warn).not.toHaveBeenCalled();
    });

    it('warns about only the first invalid plain field, capitalized', () => {
      const result = service.isCheckForm({ name: true, email: false, phone: false });

      expect(result).toBe(true);
      expect(localization.instant).toHaveBeenCalledWith('AbpValidation::Email');
      expect(localization.instant).not.toHaveBeenCalledWith('AbpValidation::Phone');
      expect(toaster.warn).toHaveBeenCalledTimes(1);
      expect(toaster.warn).toHaveBeenCalledWith('"AbpValidation::Email" AbpValidation::ThisFieldIsNotValid.');
    });

    it('formats a dotted field as two localized segments, neither re-capitalized', () => {
      // Unlike the plain-field branch above, this one does not capitalize parts[0]/parts[1], and the
      // message has no space before the suffix - documenting the actual current format, not a spec.
      service.isCheckForm({ 'address.city': false });

      expect(toaster.warn).toHaveBeenCalledWith(
        '"AbpValidation::address.AbpValidation::city"AbpValidation::ThisFieldIsNotValid.',
      );
    });

    it('formats an array-indexed field with the index preserved literally', () => {
      service.isCheckForm({ 'items[2].name': false });

      expect(toaster.warn).toHaveBeenCalledWith(
        '"AbpValidation::items[2].AbpValidation::name"AbpValidation::ThisFieldIsNotValid.',
      );
    });

    it('localizes under a custom resource module when given one', () => {
      service.isCheckForm({ email: false }, 'MyApp');

      expect(localization.instant).toHaveBeenCalledWith('MyApp::Email');
    });
  });

  describe('getFormValidationStatus', () => {
    it('flattens a nested FormGroup into dotted keys mapped to validity', () => {
      const form = new FormGroup({
        name: new FormControl('', Validators.required),
        address: new FormGroup({
          city: new FormControl('Springfield'),
          zip: new FormControl('', Validators.required),
        }),
      });

      expect(service.getFormValidationStatus(form)).toEqual({
        name: false,
        'address.city': true,
        'address.zip': false,
      });
    });

    it('flattens a FormArray into bracket-indexed keys', () => {
      const form = new FormArray([
        new FormControl('a', Validators.required),
        new FormControl('', Validators.required),
      ]);

      expect(service.getFormValidationStatus(form)).toEqual({
        '[0]': true,
        '[1]': false,
      });
    });

    it('combines group and array nesting into one flat key', () => {
      const form = new FormGroup({
        items: new FormArray([new FormGroup({ name: new FormControl('', Validators.required) })]),
      });

      expect(service.getFormValidationStatus(form)).toEqual({
        'items[0].name': false,
      });
    });
  });
});
