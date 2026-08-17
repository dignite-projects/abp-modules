import { Component, Input } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FieldTypeDefinition } from '../field-types';
import { FlexFieldData } from '../models';
import { provideFlexFields, provideFlexFieldTypes } from '../providers';
import { FlexFieldConfigComponent } from './flex-field-config.component';

@Component({ selector: 'ff-stub-config', template: '<span>stub-config</span>' })
class StubConfigComponent {
  @Input() type?: string;
  @Input() selected?: FlexFieldData;
  @Input() Entity?: FormGroup;
}

@Component({ selector: 'ff-stub-config-2', template: '<span>stub-config-2</span>' })
class StubConfig2Component {
  @Input() type?: string;
  @Input() selected?: FlexFieldData;
  @Input() Entity?: FormGroup;
}

const stubType: FieldTypeDefinition = {
  name: 'Stub',
  displayNameKey: 'FlexFields::FieldType:Text',
  configComponent: StubConfigComponent,
};
const stub2Type: FieldTypeDefinition = {
  name: 'Stub2',
  displayNameKey: 'FlexFields::FieldType:Text',
  configComponent: StubConfig2Component,
};

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'colour',
    displayName: 'Colour',
    fieldTypeName: 'Stub',
    configuration: {},
    ...overrides,
  };
}

describe('FlexFieldConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideFlexFieldTypes(stubType, stub2Type)] });
  });

  function render(type: string, form: FormGroup, selected?: FlexFieldData) {
    const fixture = TestBed.createComponent(FlexFieldConfigComponent);
    fixture.componentRef.setInput('type', type);
    fixture.componentRef.setInput('form', form);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return fixture;
  }

  it('renders the config editor registered for the field type', () => {
    const form = new FormGroup({ fieldTypeName: new FormControl('Stub') });
    const fixture = render('Stub', form);

    expect(fixture.nativeElement.textContent).toContain('stub-config');
  });

  it('renders nothing for an unregistered field type instead of throwing', () => {
    const form = new FormGroup({ fieldTypeName: new FormControl('NotRegistered') });
    const fixture = render('NotRegistered', form);

    expect(fixture.nativeElement.querySelectorAll('*').length).toBe(0);
  });

  it("swaps to the newly selected type's editor when the form's fieldTypeName control changes", () => {
    // Driven by subscribing to the form control's own valueChanges, independent of the `type` input
    // ever changing - this is how the designer UI swaps editors as the user picks a different type.
    const form = new FormGroup({ fieldTypeName: new FormControl('Stub') });
    const fixture = render('Stub', form);
    expect(fixture.nativeElement.textContent).toContain('stub-config');

    form.get('fieldTypeName')!.setValue('Stub2');

    expect(fixture.nativeElement.textContent).toContain('stub-config-2');
    expect(fixture.nativeElement.textContent).not.toContain('stub-config<');
  });

  it('passes a clone of the selected field to the editor, not the live object', () => {
    const form = new FormGroup({ fieldTypeName: new FormControl('Stub') });
    const selected = fieldData();
    const fixture = render('Stub', form, selected);

    const child = fixture.debugElement.query(By.directive(StubConfigComponent))
      .componentInstance as StubConfigComponent;
    child.selected!.displayName = 'Mutated';

    expect(selected.displayName).toBe('Colour');
  });
});

describe('built-in field config editors', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideFlexFields()],
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  for (const fieldTypeName of ['Text', 'Number', 'DateTime', 'Select', 'Boolean', 'Tree']) {
    it(`creates the ${fieldTypeName} config editor without throwing`, () => {
      const form = new FormGroup({ fieldTypeName: new FormControl(fieldTypeName) });
      const fixture = TestBed.createComponent(FlexFieldConfigComponent);
      fixture.componentRef.setInput('type', fieldTypeName);
      fixture.componentRef.setInput('form', form);

      expect(() => fixture.detectChanges()).not.toThrow();
      fixture.destroy();
    });
  }
});
