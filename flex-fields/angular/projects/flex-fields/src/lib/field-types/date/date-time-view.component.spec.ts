import { TestBed } from '@angular/core/testing';
import { ConfigStateService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { FlexFieldValue } from '../../models';
import { DateTimeViewComponent } from './date-time-view.component';
import { DateTimeInputMode } from './date-time-input-mode';

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'publishedAt',
      displayName: 'Published',
      fieldTypeName: 'DateTime',
      configuration: {},
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function withMode(mode: DateTimeInputMode): FlexFieldValue {
  return fieldValue({
    field: {
      id: '1',
      name: 'publishedAt',
      displayName: 'Published',
      fieldTypeName: 'DateTime',
      configuration: { 'DateTime.InputMode': mode },
    },
  });
}

describe('DateTimeViewComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
    });
    // shortDateTime reads the format pattern from config state; withConfig() skips the real app
    // config fetch, so the pattern has to be seeded by hand or the pipe throws on a null pattern.
    // Cast: only the two patterns the pipe reads matter here, not the rest of the DTO shape.
    TestBed.inject(ConfigStateService).setState({
      localization: {
        currentCulture: {
          dateTimeFormat: { shortDatePattern: 'M/d/yyyy', shortTimePattern: 'h:mm:ss a' },
        },
      },
    } as unknown as Parameters<ConfigStateService['setState']>[0]);
  });

  function render(value: unknown, options: { showInList?: boolean; fields?: FlexFieldValue } = {}) {
    const fixture = TestBed.createComponent(DateTimeViewComponent);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('showInList', options.showInList ?? false);
    if (options.fields) {
      fixture.componentRef.setInput('fields', options.fields);
    }
    fixture.detectChanges();
    return fixture;
  }

  // Local-time construction, not an ISO `Z` string: DatePipe formats in local time, so building the
  // expectation from the same local wall-clock value keeps the test independent of the machine's TZ.
  const localDateTime = new Date(2026, 7, 17, 10, 30, 0);

  it('renders nothing for an unset value instead of throwing', () => {
    expect(() => render('')).not.toThrow();
  });

  it('falls back to the short date-time format when no configuration is available', () => {
    expect(render(localDateTime, { showInList: true }).nativeElement.textContent).toContain('2026');
    expect(render(localDateTime, { showInList: false }).nativeElement.textContent).toContain('2026');
  });

  it('formats the value to the Date input mode, without a time part', () => {
    const text = render(localDateTime, { fields: withMode(DateTimeInputMode.Date) }).nativeElement.textContent;

    expect(text).toContain('2026-08-17');
    expect(text).not.toContain('10:30');
  });

  it('formats the value to the DateTime input mode', () => {
    const text = render(localDateTime, { fields: withMode(DateTimeInputMode.DateTime) }).nativeElement.textContent;

    expect(text).toContain('2026-08-17 10:30:00');
  });

  it('formats the value to the Month input mode, without a day part', () => {
    const text = render(localDateTime, { fields: withMode(DateTimeInputMode.Month) }).nativeElement.textContent;

    expect(text).toContain('2026-08');
    expect(text).not.toContain('2026-08-17');
  });

  it('applies the configured input mode in list mode too, e.g. a Table-nested column', () => {
    const text = render(localDateTime, {
      showInList: true,
      fields: withMode(DateTimeInputMode.Date),
    }).nativeElement.textContent;

    expect(text).toContain('2026-08-17');
    expect(text).not.toContain('10:30');
  });
});
