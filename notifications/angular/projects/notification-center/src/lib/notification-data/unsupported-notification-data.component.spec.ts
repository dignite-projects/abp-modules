import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { UnsupportedNotificationDataComponent } from './unsupported-notification-data.component';

describe('UnsupportedNotificationDataComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
    });
  });

  it('renders a safe fallback message instead of the raw, possibly-malformed payload', () => {
    const fixture = TestBed.createComponent(UnsupportedNotificationDataComponent);
    fixture.componentRef.setInput('data', { anything: 'could be here' });

    expect(() => fixture.detectChanges()).not.toThrow();
    // CoreTestingModule has no real resource loaded, so the mocked pipe degrades to the bare key
    // (the `NotificationCenter::` resource prefix is stripped) rather than a translated string.
    expect(fixture.nativeElement.textContent).toContain('UnsupportedNotification');
  });

  it('renders the same fallback even when there is no data at all', () => {
    const fixture = TestBed.createComponent(UnsupportedNotificationDataComponent);

    expect(() => fixture.detectChanges()).not.toThrow();
    // CoreTestingModule has no real resource loaded, so the mocked pipe degrades to the bare key
    // (the `NotificationCenter::` resource prefix is stripped) rather than a translated string.
    expect(fixture.nativeElement.textContent).toContain('UnsupportedNotification');
  });
});
