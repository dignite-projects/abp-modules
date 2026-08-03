import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '../field-type-config-base';
import { BooleanConfiguration } from './boolean-configuration';

let nextCheckboxId = 0;

/** Designer-side editor for a `Switch` field's configuration. */
@Component({
  selector: 'ff-boolean-config',
  templateUrl: './boolean-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class BooleanConfigComponent extends FieldTypeConfigBase {
  readonly defaultValueId = `ff-switch-default-${nextCheckboxId++}`;

  protected configurationDefaults(): object {
    return new BooleanConfiguration();
  }
}
