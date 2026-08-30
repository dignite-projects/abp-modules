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

    // Contract coverage beyond "does it throw": every built-in editor extends FieldTypeConfigBase,
    // whose `Entity`/`type`/`selected` are inherited *setter* @Input()s, reached via setDynamicInputs
    // rather than ComponentRef.setInput() directly - see that function's own doc comment for why.
    // This test cannot reproduce the failure setDynamicInputs guards against: that gap is specific to
    // a subclass of FieldTypeConfigBase compiled downstream, in a *different* compilation unit, from a
    // published copy of this library - which is not something any test inside this repo's own build can
    // set up (everything here, built-ins included, compiles together as one program). It was confirmed
    // directly against a real downstream consumer instead: opening an existing field for edit rendered
    // its stored configuration as blank defaults, and (unnoticed) Save would have re-persisted that
    // emptied-out configuration over what was actually stored. This test still earns its place as a
    // contract check on FlexFieldConfigComponent's dispatch - it would catch a *different* way of
    // breaking the same handoff, even one this in-repo setup can exercise.
    it(`restores the ${fieldTypeName} editor's own form group onto the host form`, () => {
      const form = new FormGroup({ fieldTypeName: new FormControl(fieldTypeName) });
      const fixture = TestBed.createComponent(FlexFieldConfigComponent);
      fixture.componentRef.setInput('type', fieldTypeName);
      fixture.componentRef.setInput('form', form);

      fixture.detectChanges();

      // Every FieldTypeConfigBase subclass's rebuild() does formEntity.setControl('configuration', ...)
      // as its very first act once Entity/type both land - so this key existing on the *same* form
      // instance the caller holds is proof the inherited setter actually ran, not just that some
      // component of the right type was instantiated.
      expect(form.get('configuration')).toBeTruthy();
      fixture.destroy();
    });
  }
});
