import { TestBed } from '@angular/core/testing';
import { MessageNotificationDataComponent } from './message-notification-data.component';

describe('MessageNotificationDataComponent', () => {
  function render(data: unknown) {
    const fixture = TestBed.createComponent(MessageNotificationDataComponent);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the pre-formatted message text', () => {
    const fixture = render({ message: 'Your order has shipped.' });
    expect(fixture.nativeElement.textContent).toContain('Your order has shipped.');
  });

  it('renders nothing rather than throwing when there is no data', () => {
    expect(() => render(null)).not.toThrow();
    expect(render(null).nativeElement.textContent.trim()).toBe('');
  });
});
