import { FormArray } from '@angular/forms';

/**
 * Configuration of a `Matrix` field, shaped for `FormBuilder.group()`. Mirrors `MatrixConfiguration`
 * (`src/Dignite.Abp.FlexFields.Abstractions/Dignite/Abp/FlexFields/Matrix/MatrixConfiguration.cs`).
 */
export class MatrixConfiguration {
  'Matrix.BlockTypes': unknown = new FormArray<never>([]);
}
