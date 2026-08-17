import { TestBed } from '@angular/core/testing';
import { TextViewComponent } from './text-view.component';

describe('TextViewComponent', () => {
  function render(value: unknown, showInList = false) {
    const fixture = TestBed.createComponent(TextViewComponent);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('showInList', showInList);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the raw value bare in list mode', () => {
    expect(render('hello', true).nativeElement.textContent.trim()).toBe('hello');
  });

  it('renders the raw value inside the label wrapper otherwise', () => {
    expect(render('hello', false).nativeElement.textContent).toContain('hello');
  });
});
