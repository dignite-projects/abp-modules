import { TestBed } from '@angular/core/testing';
import { LocalizationService } from '@abp/ng.core';
import { LocalizableMessageNotificationDataComponent } from './localizable-message-notification-data.component';

describe('LocalizableMessageNotificationDataComponent', () => {
  const localization = { instant: vi.fn((key: string, ...args: string[]) => `${key}(${args.join(',')})`) };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [{ provide: LocalizationService, useValue: localization }],
    });
  });

  function render(data: unknown) {
    const fixture = TestBed.createComponent(LocalizableMessageNotificationDataComponent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();
    return fixture;
  }

  it('localizes by resource + name when a resource name is given', () => {
    const fixture = render({ resourceName: 'Demo', name: 'OrderShipped' });

    expect(localization.instant).toHaveBeenCalledWith('Demo::OrderShipped');
    expect(fixture.nativeElement.textContent).toContain('Demo::OrderShipped()');
  });

  it('localizes by name alone when no resource name is given', () => {
    render({ name: 'OrderShipped' });

    expect(localization.instant).toHaveBeenCalledWith('OrderShipped');
  });

  it('passes positional arguments through to the localizer in order', () => {
    render({ name: 'OrderShipped', arguments: { '0': '#1234', '1': 'today' } });

    expect(localization.instant).toHaveBeenCalledWith('OrderShipped', '#1234', 'today');
  });

  it('renders empty rather than throwing when there is no name to localize', () => {
    const fixture = render({});

    expect(localization.instant).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent.trim()).toBe('');
  });

  it('renders empty rather than throwing when there is no data at all', () => {
    expect(() => render(null)).not.toThrow();
  });
});
