import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { BooleanViewComponent } from './boolean-view.component';

describe('BooleanViewComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
    });
  });

  function render(value: unknown, showInList = false) {
    const fixture = TestBed.createComponent(BooleanViewComponent);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('showInList', showInList);
    fixture.detectChanges();
    return fixture;
  }

  it('renders Yes for a true value', () => {
    const fixture = render(true);
    expect(fixture.nativeElement.textContent).toContain('Yes');
  });

  it('renders No for a false value', () => {
    const fixture = render(false);
    expect(fixture.nativeElement.textContent).toContain('No');
  });
});
