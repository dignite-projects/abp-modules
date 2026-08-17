import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '../../models';
import { SelectConfigComponent } from './select-config.component';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'color',
    displayName: 'Color',
    fieldTypeName: 'Select',
    configuration: {},
    ...overrides,
  };
}

describe('SelectConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  function render(selected?: FlexFieldData) {
    const entity = new FormGroup({});
    const fixture = TestBed.createComponent(SelectConfigComponent);
    fixture.componentRef.setInput('type', 'Select');
    fixture.componentRef.setInput('Entity', entity);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, entity, component: fixture.componentInstance };
  }

  it('starts a new field with one blank option row, not an empty table', () => {
    const { component } = render();

    expect(component.options.length).toBe(1);
    expect(component.options.at(0).value).toEqual({ Text: '', Value: '', Selected: false });
  });

  it('seeds scalar defaults for a new field', () => {
    const { entity } = render();

    expect(entity.get(['configuration', 'Select.NullText'])!.value).toBe('');
    expect(entity.get(['configuration', 'Select.Multiple'])!.value).toBe(false);
  });

  it('patches in the stored options when editing a field of this type', () => {
    const { component } = render(
      fieldData({
        configuration: {
          'Select.Options': [
            { Text: 'Red', Value: 'red', Selected: false },
            { Text: 'Blue', Value: 'blue', Selected: true },
          ],
        },
      }),
    );

    expect(component.options.length).toBe(2);
    expect(component.options.value).toEqual([
      { Text: 'Red', Value: 'red', Selected: false },
      { Text: 'Blue', Value: 'blue', Selected: true },
    ]);
  });

  it('normalizes camelCase stored options the same as PascalCase', () => {
    const { component } = render(
      fieldData({ configuration: { 'Select.Options': [{ text: 'Red', value: 'red', selected: true }] } }),
    );

    expect(component.options.value).toEqual([{ Text: 'Red', Value: 'red', Selected: true }]);
  });

  it('does not leak configuration from a field of a different type', () => {
    const { component, entity } = render(
      fieldData({
        fieldTypeName: 'Text',
        configuration: { 'Select.Options': [{ Text: 'Red', Value: 'red', Selected: true }], 'Select.Multiple': true },
      }),
    );

    expect(component.options.length).toBe(1);
    expect(component.options.at(0).value).toEqual({ Text: '', Value: '', Selected: false });
    expect(entity.get(['configuration', 'Select.Multiple'])!.value).toBe(false);
  });

  it('adds and removes option rows', () => {
    const { component } = render();
    expect(component.options.length).toBe(1);

    component.addOption();
    expect(component.options.length).toBe(2);

    component.deleteOption(0);
    expect(component.options.length).toBe(1);
  });

  it('seeds Value from Text for a blank row', () => {
    const { component } = render();

    component.onTextChange({ target: { value: 'Red' } } as unknown as Event, 0);

    expect(component.options.at(0).value).toEqual({ Text: '', Value: 'Red', Selected: false });
  });

  it('never overwrites a Value the user already typed', () => {
    const { component } = render();
    component.options.at(0).patchValue({ Value: 'custom-value' });

    component.onTextChange({ target: { value: 'Red' } } as unknown as Event, 0);

    expect(component.options.at(0).value.Value).toBe('custom-value');
  });

  it('reorders option rows on drop', () => {
    const { component } = render(
      fieldData({
        configuration: {
          'Select.Options': [
            { Text: 'Red', Value: 'red', Selected: false },
            { Text: 'Blue', Value: 'blue', Selected: false },
            { Text: 'Green', Value: 'green', Selected: false },
          ],
        },
      }),
    );

    component.drop({ previousIndex: 0, currentIndex: 2 } as CdkDragDrop<unknown>);

    expect(component.options.value.map((option: { Value: string }) => option.Value)).toEqual([
      'blue',
      'green',
      'red',
    ]);
  });
});
