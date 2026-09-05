import { FormBuilder } from '@angular/forms';
import { BUILT_IN_FIELD_TYPES } from './built-in-field-types';
import { BooleanConfiguration } from './boolean';
import { DateTimeConfiguration } from './date';
import { MatrixConfiguration } from './matrix';
import { NumberConfiguration } from './number';
import { SelectConfiguration } from './select';
import { TableConfiguration } from './table';
import { TextConfiguration } from './text';
import { TreeConfiguration } from './tree';

/**
 * These are wire-contract tests, not coverage.
 *
 * Every string asserted below is a **stored value** shared with the server: the eight registration keys
 * live in `IFlexFieldData.FieldTypeName`, and the configuration keys are the literal keys of
 * `FieldConfigurationDictionary`. Renaming any of them here — however tidy the new name looks —
 * silently orphans every field already saved under the old one, and nothing else in the build would
 * catch it. If one of these fails, the fix is almost never to update the expectation.
 */
describe('built-in field types', () => {
  it('registers exactly the eight the server ships, under the server keys', () => {
    expect(BUILT_IN_FIELD_TYPES.map(fieldType => fieldType.name)).toEqual([
      'Text',
      'Number',
      'DateTime',
      'Select',
      'Boolean',
      'Tree',
      'Matrix',
      'Table',
    ]);
  });

  it('resolves each key to a localization key, not a hard-coded label', () => {
    for (const fieldType of BUILT_IN_FIELD_TYPES) {
      expect(fieldType.displayNameKey).toMatch(/^FlexFields::FieldType:/);
    }
  });

  it('gives every type a control and a view component', () => {
    for (const fieldType of BUILT_IN_FIELD_TYPES) {
      expect(fieldType.controlComponent).toBeDefined();
      expect(fieldType.viewComponent).toBeDefined();
      expect(fieldType.configComponent).toBeDefined();
    }
  });

  it('has a search component for every type except DateTime, Matrix and Table', () => {
    const withoutSearch = BUILT_IN_FIELD_TYPES.filter(
      fieldType => !fieldType.searchComponent,
    ).map(fieldType => fieldType.name);

    // DateTime is a tracked gap, not an oversight: the server indexes it and allows six operators on
    // it, so a date range filter is a real thing to build — it just was never part of what was
    // migrated. Matrix and Table are the opposite — permanent: `IndexValueType` is null for both on
    // the server, so their values never reach the query index and there is nothing to filter on.
    expect(withoutSearch).toEqual(['DateTime', 'Matrix', 'Table']);
  });

  it('marks exactly Matrix and Table as composite', () => {
    // The flag exists so a composite config editor can stop offering composite types once
    // CompositeFieldNesting.MaxDepth is reached. Every scalar type must leave it unset.
    const composite = BUILT_IN_FIELD_TYPES.filter(fieldType => fieldType.composite === true).map(
      fieldType => fieldType.name,
    );

    expect(composite).toEqual(['Matrix', 'Table']);
  });

  it('cannot be mutated at runtime', () => {
    expect(Object.isFrozen(BUILT_IN_FIELD_TYPES)).toBe(true);
  });
});

describe('configuration keys', () => {
  const keysOf = (configuration: object) => Object.keys(new FormBuilder().group(configuration).value);

  it('Text', () => {
    expect(keysOf(new TextConfiguration())).toEqual([
      'Text.Placeholder',
      'Text.Mode',
      'Text.CharLimit',
    ]);
  });

  it('Number — note the unprefixed FormatSpecifier', () => {
    expect(keysOf(new NumberConfiguration())).toEqual([
      'Number.Placeholder',
      'Number.Min',
      'Number.Max',
      'Number.Decimals',
      'Number.Step',
      'FormatSpecifier',
    ]);
  });

  it('DateTime', () => {
    expect(keysOf(new DateTimeConfiguration())).toEqual([
      'DateTime.InputMode',
      'DateTime.Min',
      'DateTime.Max',
    ]);
  });

  it('Select', () => {
    expect(keysOf(new SelectConfiguration())).toEqual([
      'Select.NullText',
      'Select.Multiple',
      'Select.Size',
      'Select.Options',
    ]);
  });

  it('Boolean', () => {
    expect(keysOf(new BooleanConfiguration())).toEqual(['Boolean.Default']);
  });

  it('Tree', () => {
    expect(keysOf(new TreeConfiguration())).toEqual(['Tree.Multiple', 'Tree.Nodes']);
  });

  it('Matrix', () => {
    expect(keysOf(new MatrixConfiguration())).toEqual(['Matrix.BlockTypes']);
  });

  it('Table', () => {
    expect(keysOf(new TableConfiguration())).toEqual(['Table.Columns']);
  });

  it('seeds Text.CharLimit with the server default of 256', () => {
    const value = new FormBuilder().group(new TextConfiguration()).value;
    expect(value['Text.CharLimit']).toBe(256);
  });

  it('leaves numeric bounds optional', () => {
    // .controls[key], not .get(key): AbstractControl.get treats '.' as a path separator, so
    // get('Number.Min') looks for a *nested group* called Number and finds null. Every
    // configuration key here contains a dot, so this distinction is not incidental.
    const group = new FormBuilder().group(new NumberConfiguration());

    // The old library made Min and Max required, so no numeric field could be saved without bounds.
    expect(group.controls['Number.Min'].valid).toBe(true);
    expect(group.controls['Number.Max'].valid).toBe(true);
  });
});
