import { FormArray } from '@angular/forms';

/**
 * Configuration of a `Select` field, shaped for `FormBuilder.group()`. Mirrors `SelectConfiguration`
 * on the server.
 */
export class SelectConfiguration {
  'Select.NullText': unknown = [''];

  'Select.Multiple': unknown = [false];

  // Present on the server but missing from the old Angular library, so a size configured through any
  // other client was dropped the moment the field was edited here.
  'Select.Size': unknown = [null];

  'Select.Options': unknown = new FormArray<never>([]);
}
