import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '../field-type-config-base';
import { NumberConfiguration } from './number-configuration';

/** Designer-side editor for a `NumericEdit` field's configuration. */
@Component({
  selector: 'ff-number-config',
  templateUrl: './number-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class NumberConfigComponent extends FieldTypeConfigBase {
  /** `FormatSpecifier` takes a .NET standard numeric format string, which the server applies. */
  readonly formatSpecifierDocsUrl =
    'https://learn.microsoft.com/dotnet/standard/base-types/standard-numeric-format-strings';

  protected configurationDefaults(): object {
    return new NumberConfiguration();
  }
}
