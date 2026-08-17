import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '../../models';
import { DateTimeConfigComponent } from './date-time-config.component';
import { DateTimeInputMode } from './date-time-input-mode';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'publishedAt',
    displayName: 'Published',
    fieldTypeName: 'DateTime',
    configuration: {},
    ...overrides,
  };
}

describe('DateTimeConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  function render(selected?: FlexFieldData) {
    const entity = new FormGroup({});
    const fixture = TestBed.createComponent(DateTimeConfigComponent);
    fixture.componentRef.setInput('type', 'DateTime');
    fixture.componentRef.setInput('Entity', entity);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, entity };
  }

  it('seeds the configuration group with Date mode and empty bounds for a new field', () => {
    const { entity } = render();

    expect(entity.get(['configuration', 'DateTime.InputMode'])!.value).toBe(DateTimeInputMode.Date);
    expect(entity.get(['configuration', 'DateTime.Min'])!.value).toBe('');
    expect(entity.get(['configuration', 'DateTime.Max'])!.value).toBe('');
  });

  it('does not touch DatePipe for a fresh field, so an unset Min/Max never throws', () => {
    // onInputModeChange (which reformats Min/Max through DatePipe) only runs after patching a stored
    // field's configuration - a fresh create must not call it against the still-empty defaults.
    const { fixture } = render();
    expect(fixture.componentInstance.dateTimeType).toBe('date');
  });

  it('patches in the stored mode and bounds when editing a field of this type', () => {
    const { entity } = render(
      fieldData({
        configuration: {
          'DateTime.InputMode': DateTimeInputMode.DateTime,
          'DateTime.Min': '2026-01-01 00:00:00',
          'DateTime.Max': '2026-12-31 23:59:59',
        },
      }),
    );

    expect(entity.get(['configuration', 'DateTime.InputMode'])!.value).toBe(DateTimeInputMode.DateTime);
    expect(entity.get(['configuration', 'DateTime.Min'])!.value).toBe('2026-01-01 00:00:00');
  });

  it('reformats Min/Max and the input type when the mode changes', () => {
    const { fixture, entity } = render(
      fieldData({
        configuration: { 'DateTime.InputMode': DateTimeInputMode.Date, 'DateTime.Min': '2026-08-17' },
      }),
    );
    expect(fixture.componentInstance.dateTimeType).toBe('date');

    entity.get(['configuration', 'DateTime.InputMode'])!.setValue(DateTimeInputMode.Month);
    fixture.componentInstance.onInputModeChange();

    expect(fixture.componentInstance.dateTimeType).toBe('month');
    expect(entity.get(['configuration', 'DateTime.Min'])!.value).toBe('2026-08');
  });

  it('does not leak configuration from a field of a different type', () => {
    const { entity } = render(
      fieldData({
        fieldTypeName: 'Text',
        configuration: { 'DateTime.InputMode': DateTimeInputMode.Month },
      }),
    );

    expect(entity.get(['configuration', 'DateTime.InputMode'])!.value).toBe(DateTimeInputMode.Date);
  });

  it('gives every instance a distinct, but internally consistent, set of radio ids', () => {
    const first = render().fixture.componentInstance.radioIds;
    const second = render().fixture.componentInstance.radioIds;
    const suffix = (id: string) => id.match(/-(\d+)$/)?.[1];

    // Same instance: the three radios share one numeric suffix (see the source comment on
    // `nextRadioId`) - only the prefix tells them apart.
    expect(first[DateTimeInputMode.Date]).not.toBe(first[DateTimeInputMode.DateTime]);
    expect(suffix(first[DateTimeInputMode.Date])).toBe(suffix(first[DateTimeInputMode.DateTime]));
    expect(suffix(first[DateTimeInputMode.Date])).toBe(suffix(first[DateTimeInputMode.Month]));

    // Different instances: the suffix itself must differ, or their <label for> would collide.
    expect(suffix(first[DateTimeInputMode.Date])).not.toBe(suffix(second[DateTimeInputMode.Date]));
  });
});
