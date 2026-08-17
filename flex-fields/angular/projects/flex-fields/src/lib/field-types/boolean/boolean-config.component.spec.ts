import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '../../models';
import { BooleanConfigComponent } from './boolean-config.component';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'isActive',
    displayName: 'Active',
    fieldTypeName: 'Boolean',
    configuration: {},
    ...overrides,
  };
}

describe('BooleanConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  function render(selected?: FlexFieldData) {
    const entity = new FormGroup({});
    const fixture = TestBed.createComponent(BooleanConfigComponent);
    fixture.componentRef.setInput('type', 'Boolean');
    fixture.componentRef.setInput('Entity', entity);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, entity };
  }

  it('seeds the configuration group with the default when creating a new field', () => {
    const { entity } = render();
    expect(entity.get('configuration')!.value).toEqual({ 'Boolean.Default': false });
  });

  it('patches in the stored value when editing a field of this type', () => {
    const { entity } = render(fieldData({ configuration: { 'Boolean.Default': true } }));
    expect(entity.get(['configuration', 'Boolean.Default'])!.value).toBe(true);
  });

  it('does not leak configuration from a field of a different type', () => {
    // The designer keeps every type's config editor around and swaps which one is shown, so a
    // stale selection of a different type must not patch its keys into this one's group.
    const { entity } = render(
      fieldData({ fieldTypeName: 'Text', configuration: { 'Boolean.Default': true } }),
    );
    expect(entity.get(['configuration', 'Boolean.Default'])!.value).toBe(false);
  });

  it('gives every instance a distinct checkbox id', () => {
    const first = render().fixture.componentInstance.defaultValueId;
    const second = render().fixture.componentInstance.defaultValueId;
    expect(first).not.toBe(second);
  });
});
