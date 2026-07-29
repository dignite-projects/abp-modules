import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '../field-type-config-base';
import { TextConfiguration } from './text-configuration';
import { TextMode } from './text-mode';

let nextRadioId = 0;

/** Designer-side editor for a `TextEdit` field's configuration. */
@Component({
  selector: 'ff-text-config',
  templateUrl: './text-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class TextConfigComponent extends FieldTypeConfigBase {
  readonly TextMode = TextMode;

  // Radio inputs need ids unique across every config editor on the page, and there can be several.
  readonly singleLineId = `ff-text-mode-single-${nextRadioId++}`;
  readonly multipleLineId = `ff-text-mode-multiple-${nextRadioId++}`;

  protected configurationDefaults(): object {
    return new TextConfiguration();
  }
}
