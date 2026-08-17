import { TestBed } from '@angular/core/testing';
import { ConfigStateService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { DateTimeViewComponent } from './date-time-view.component';

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

  function render(value: unknown, showInList = false) {
    const fixture = TestBed.createComponent(DateTimeViewComponent);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('showInList', showInList);
    fixture.detectChanges();
    return fixture;
  }

  it('renders nothing for an unset value instead of throwing', () => {
    expect(() => render('')).not.toThrow();
  });

  it('renders a formatted value both in and out of list mode', () => {
    const localDateTime = new Date(2026, 7, 17, 10, 30, 0);

    expect(render(localDateTime, true).nativeElement.textContent).toContain('2026');
    expect(render(localDateTime, false).nativeElement.textContent).toContain('2026');
  });
});
