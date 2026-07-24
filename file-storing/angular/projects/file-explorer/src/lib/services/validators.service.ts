import { LocalizationService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { Injectable } from '@angular/core';
import { FormArray, FormControl, FormGroup } from '@angular/forms';

@Injectable({
  providedIn: 'root',
})
export class ValidatorsService {
  constructor(private toaster: ToasterService, private localizationService: LocalizationService) {}

  isCheckForm(input: { [key: string]: boolean }, module = 'AbpValidation'): boolean {
    const keys = Object.keys(input);
    for (const element of keys) {
      if (input[element] === false) {
        const displayName = element.charAt(0).toUpperCase() + element.slice(1);
        let info = `"${this.localizationService.instant(`${module}::${displayName}`)}" `;

        if (element.includes('.') && !element.includes('].')) {
          const parts = element.split('.');
          info = `"${this.localizationService.instant(`${module}::${parts[0]}`)}.${this.localizationService.instant(`${module}::${parts[1]}`)}"`;
        }

        if (element.includes('].')) {
          const parts = element.split('].');
          const arrayStart = parts[0].split('[');
          info = `"${this.localizationService.instant(`${module}::${arrayStart[0]}`)}[${arrayStart[1]}].${this.localizationService.instant(`${module}::${parts[1]}`)}"`;
        }

        this.toaster.warn(info + this.localizationService.instant('AbpValidation::ThisFieldIsNotValid.'));
        return true;
      }
    }

    return false;
  }

  getFormValidationStatus(formEntity: FormGroup | FormArray): { [key: string]: any } {
    const validationStatus: { [key: string]: any } = {};

    const traverseForm = (form: FormGroup | FormArray, prefix = '') => {
      if (form instanceof FormGroup) {
        Object.keys(form.controls).forEach(key => {
          const control = form.controls[key];
          const fullKey = prefix ? `${prefix}.${key}` : key;
          if (control instanceof FormControl) {
            validationStatus[fullKey] = control.valid;
          } else if (control instanceof FormArray || control instanceof FormGroup) {
            traverseForm(control, fullKey);
          }
        });
      } else {
        form.controls.forEach((control, index) => {
          const fullKey = prefix ? `${prefix}[${index}]` : `[${index}]`;
          if (control instanceof FormControl) {
            validationStatus[fullKey] = control.valid;
          } else if (control instanceof FormArray || control instanceof FormGroup) {
            traverseForm(control, fullKey);
          }
        });
      }
    };

    traverseForm(formEntity);

    return validationStatus;
  }
}
