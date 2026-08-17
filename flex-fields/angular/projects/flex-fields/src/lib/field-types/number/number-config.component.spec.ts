import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '../../models';
import { NumberConfigComponent } from './number-config.component';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'price',
    displayName: 'Price',
    fieldTypeName: 'Number',
    configuration: {},
    ...overrides,
  };
}

describe('NumberConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  function render(selected?: FlexFieldData) {
    const entity = new FormGroup({});
    const fixture = TestBed.createComponent(NumberConfigComponent);
    fixture.componentRef.setInput('type', 'Number');
    fixture.componentRef.setInput('Entity', entity);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, entity };
  }

  it('seeds the configuration group with the server defaults for a new field', () => {
    const { entity } = render();
    const configuration = entity.get('configuration')!.value;

    expect(configuration).toEqual({
      'Number.Placeholder': '',
      'Number.Min': null,
      'Number.Max': null,
      'Number.Decimals': 2,
      'Number.Step': null,
      FormatSpecifier: '',
    });
  });

  it('patches in the stored bounds when editing a field of this type', () => {
    const { entity } = render(
      fieldData({
        configuration: { 'Number.Min': 0, 'Number.Max': 999, 'Number.Decimals': 0 },
      }),
    );

    expect(entity.get(['configuration', 'Number.Min'])!.value).toBe(0);
    expect(entity.get(['configuration', 'Number.Max'])!.value).toBe(999);
    expect(entity.get(['configuration', 'Number.Decimals'])!.value).toBe(0);
  });

  it('does not leak configuration from a field of a different type', () => {
    const { entity } = render(
      fieldData({ fieldTypeName: 'Text', configuration: { 'Number.Decimals': 5 } }),
    );

    expect(entity.get(['configuration', 'Number.Decimals'])!.value).toBe(2);
  });
});
