import { Component, Input } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ConfigStateService } from '@abp/ng.core';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { FieldTypeDefinition } from '../field-types';
import { FlexFieldValue } from '../models';
import { provideFlexFields, provideFlexFieldTypes } from '../providers';
import { FlexFieldViewComponent } from './flex-field-view.component';

@Component({ selector: 'ff-stub-view', template: '<span>{{ value }}</span>' })
class StubViewComponent {
  @Input() fields?: FlexFieldValue;
  @Input() type?: string;
  @Input() value: unknown;
  @Input() showInList = false;
}

const stubType: FieldTypeDefinition = {
  name: 'Stub',
  displayNameKey: 'FlexFields::FieldType:Text',
  viewComponent: StubViewComponent,
};

function fieldValue(fieldTypeName: string): FlexFieldValue {
  return {
    field: { id: '1', name: 'colour', displayName: 'Colour', fieldTypeName, configuration: {} },
    required: false,
    searchable: false,
  };
}

describe('FlexFieldViewComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideFlexFieldTypes(stubType)] });
  });

  function render(type: string | undefined, value: unknown = '') {
    const fixture = TestBed.createComponent(FlexFieldViewComponent);
    fixture.componentRef.setInput('type', type);
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the view component registered for the field type', () => {
    const fixture = render('Stub', 'hello');

    expect(fixture.nativeElement.textContent).toContain('hello');
  });

  it('renders nothing for an unregistered field type instead of throwing', () => {
    const fixture = render('NotRegistered');

    expect(fixture.nativeElement.querySelectorAll('*').length).toBe(0);
  });

  it('renders nothing when no type is given at all', () => {
    const fixture = render(undefined);

    expect(fixture.nativeElement.querySelectorAll('*').length).toBe(0);
  });

  it('still renders for an empty value - a view decides for itself how to show "nothing"', () => {
    const fixture = render('Stub', '');

    expect(fixture.nativeElement.querySelector('span')).toBeTruthy();
  });

  it('does not recreate the dynamic view when its input references are unchanged', () => {
    const fixture = TestBed.createComponent(FlexFieldViewComponent);
    fixture.componentRef.setInput('type', 'Stub');
    fixture.componentRef.setInput('value', 'hello');
    fixture.detectChanges();

    const first = fixture.componentInstance.fieldRef?.get(0);

    fixture.componentRef.setInput('type', 'Stub');
    fixture.componentRef.setInput('value', 'hello');
    fixture.detectChanges();

    expect(fixture.componentInstance.fieldRef?.get(0)).toBe(first);
  });

  it('recreates the view when the value changes', () => {
    const fixture = TestBed.createComponent(FlexFieldViewComponent);
    fixture.componentRef.setInput('type', 'Stub');
    fixture.componentRef.setInput('value', 'hello');
    fixture.detectChanges();

    const first = fixture.componentInstance.fieldRef?.get(0);

    fixture.componentRef.setInput('type', 'Stub');
    fixture.componentRef.setInput('value', 'world');
    fixture.detectChanges();

    expect(fixture.componentInstance.fieldRef?.get(0)).not.toBe(first);
  });
});

describe('built-in field views', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideFlexFields()],
      imports: [CoreTestingModule.withConfig()],
    });
    // shortDateTime (used by the DateTime view) reads its format pattern from config state;
    // withConfig() skips the real app-config fetch, so the pattern has to be seeded by hand.
    TestBed.inject(ConfigStateService).setState({
      localization: {
        currentCulture: {
          dateTimeFormat: { shortDatePattern: 'M/d/yyyy', shortTimePattern: 'h:mm:ss a' },
        },
      },
    } as unknown as Parameters<ConfigStateService['setState']>[0]);
  });

  for (const fieldTypeName of ['Text', 'Number', 'DateTime', 'Select', 'Boolean', 'Tree']) {
    it(`creates the ${fieldTypeName} view without throwing`, () => {
      const fixture = TestBed.createComponent(FlexFieldViewComponent);
      fixture.componentRef.setInput('type', fieldTypeName);
      fixture.componentRef.setInput('value', '');
      fixture.componentRef.setInput('fields', fieldValue(fieldTypeName));

      expect(() => fixture.detectChanges()).not.toThrow();
      fixture.destroy();
    });
  }
});
