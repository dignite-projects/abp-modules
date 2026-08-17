import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '../../models';
import { TextConfigComponent } from './text-config.component';
import { TextMode } from './text-mode';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'title',
    displayName: 'Title',
    fieldTypeName: 'Text',
    configuration: {},
    ...overrides,
  };
}

describe('TextConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  function render(selected?: FlexFieldData) {
    const entity = new FormGroup({});
    const fixture = TestBed.createComponent(TextConfigComponent);
    fixture.componentRef.setInput('type', 'Text');
    fixture.componentRef.setInput('Entity', entity);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, entity };
  }

  it('seeds the configuration group with the server defaults for a new field', () => {
    const { entity } = render();

    expect(entity.get('configuration')!.value).toEqual({
      'Text.Placeholder': '',
      'Text.Mode': TextMode.SingleLine,
      'Text.CharLimit': 256,
    });
  });

  it('patches in the stored configuration when editing a field of this type', () => {
    const { entity } = render(
      fieldData({
        configuration: { 'Text.Mode': TextMode.MultipleLine, 'Text.CharLimit': 1024 },
      }),
    );

    expect(entity.get(['configuration', 'Text.Mode'])!.value).toBe(TextMode.MultipleLine);
    expect(entity.get(['configuration', 'Text.CharLimit'])!.value).toBe(1024);
  });

  it('does not leak configuration from a field of a different type', () => {
    const { entity } = render(fieldData({ fieldTypeName: 'Select', configuration: { 'Text.CharLimit': 9999 } }));

    expect(entity.get(['configuration', 'Text.CharLimit'])!.value).toBe(256);
  });

  it('gives every instance distinct, stable radio ids for both modes', () => {
    const first = render().fixture.componentInstance;
    const second = render().fixture.componentInstance;

    expect(first.singleLineId).not.toBe(first.multipleLineId);
    expect(first.singleLineId).not.toBe(second.singleLineId);
    expect(first.multipleLineId).not.toBe(second.multipleLineId);
  });
});
