import { FormBuilder } from '@angular/forms';
import { BUILT_IN_FIELD_TYPES } from './built-in-field-types';
import { DateConfiguration } from './date';
import { NumericConfiguration } from './numeric';
import { SelectConfiguration } from './select';
import { SwitchConfiguration } from './switch';
import { TextConfiguration } from './text';
import { TreeViewConfiguration } from './tree-view';

/**
 * These are wire-contract tests, not coverage.
 *
 * Every string asserted below is a **stored value** shared with the server: the six registration keys
 * live in `IFlexFieldData.FieldTypeName`, and the configuration keys are the literal keys of
 * `FieldConfigurationDictionary`. Renaming any of them here — however tidy the new name looks —
 * silently orphans every field already saved under the old one, and nothing else in the build would
 * catch it. If one of these fails, the fix is almost never to update the expectation.
 */
describe('built-in field types', () => {
  it('registers exactly the six the server ships, under the server keys', () => {
    expect(BUILT_IN_FIELD_TYPES.map(fieldType => fieldType.name)).toEqual([
      'TextEdit',
      'NumericEdit',
      'DateEdit',
      'Select',
      'Switch',
      'TreeView',
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

  it('has a search component for every type except DateEdit', () => {
    const withoutSearch = BUILT_IN_FIELD_TYPES.filter(
      fieldType => !fieldType.searchComponent,
    ).map(fieldType => fieldType.name);

    // Tracked gap, not an oversight: the server indexes DateTime and allows six operators on it, so a
    // date range filter is a real thing to build — it just was never part of what was migrated.
    expect(withoutSearch).toEqual(['DateEdit']);
  });

  it('cannot be mutated at runtime', () => {
    expect(Object.isFrozen(BUILT_IN_FIELD_TYPES)).toBe(true);
  });
});

describe('configuration keys', () => {
  const keysOf = (configuration: object) => Object.keys(new FormBuilder().group(configuration).value);

  it('TextEdit', () => {
    expect(keysOf(new TextConfiguration())).toEqual([
      'TextEdit.Placeholder',
      'TextEdit.Mode',
      'TextEdit.CharLimit',
    ]);
  });

  it('NumericEdit — note the NumericEditField. prefix and the unprefixed FormatSpecifier', () => {
    expect(keysOf(new NumericConfiguration())).toEqual([
      'NumericEditField.Placeholder',
      'NumericEditField.Min',
      'NumericEditField.Max',
      'NumericEditField.Decimals',
      'NumericEditField.Step',
      'FormatSpecifier',
    ]);
  });

  it('DateEdit', () => {
    expect(keysOf(new DateConfiguration())).toEqual([
      'DateEdit.InputMode',
      'DateEdit.Min',
      'DateEdit.Max',
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

  it('Switch', () => {
    expect(keysOf(new SwitchConfiguration())).toEqual(['Switch.Default']);
  });

  it('TreeView', () => {
    expect(keysOf(new TreeViewConfiguration())).toEqual(['TreeView.Multiple', 'TreeView.Nodes']);
  });

  it('seeds TextEdit.CharLimit with the server default of 256', () => {
    const value = new FormBuilder().group(new TextConfiguration()).value;
    expect(value['TextEdit.CharLimit']).toBe(256);
  });

  it('leaves numeric bounds optional', () => {
    // .controls[key], not .get(key): AbstractControl.get treats '.' as a path separator, so
    // get('NumericEditField.Min') looks for a *nested group* called NumericEditField and finds null.
    // Every configuration key here contains a dot, so this distinction is not incidental.
    const group = new FormBuilder().group(new NumericConfiguration());

    // The old library made Min and Max required, so no numeric field could be saved without bounds.
    expect(group.controls['NumericEditField.Min'].valid).toBe(true);
    expect(group.controls['NumericEditField.Max'].valid).toBe(true);
  });
});
