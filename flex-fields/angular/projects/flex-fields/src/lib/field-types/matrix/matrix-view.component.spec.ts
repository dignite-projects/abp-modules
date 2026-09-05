import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { FlexFieldValue } from '../../models';
import { provideFlexFields } from '../../providers';
import { MatrixViewComponent } from './matrix-view.component';

const BLOCK_TYPES = [
  {
    name: 'quote',
    displayName: 'Quote',
    fields: [
      { name: 'text', displayName: 'Text', fieldTypeName: 'Text', required: false, configuration: {} },
    ],
  },
];

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'sections',
      displayName: 'Sections',
      fieldTypeName: 'Matrix',
      configuration: { 'Matrix.BlockTypes': BLOCK_TYPES },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function render(fields: FlexFieldValue, value: unknown, showInList = false) {
  const fixture = TestBed.createComponent(MatrixViewComponent);
  fixture.componentRef.setInput('fields', fields);
  fixture.componentRef.setInput('type', 'Matrix');
  fixture.componentRef.setInput('value', value);
  fixture.componentRef.setInput('showInList', showInList);
  fixture.detectChanges();
  return fixture;
}

describe('MatrixViewComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
      providers: [provideFlexFields()],
    });
  });

  it('renders each block under its block type display name', () => {
    const fixture = render(fieldValue(), [{ blockTypeName: 'quote', values: { text: 'hello' } }]);

    expect(fixture.nativeElement.textContent).toContain('Quote');
  });

  it('recurses into <ff-flex-field-view> for each sub-field value', () => {
    const fixture = render(fieldValue(), [{ blockTypeName: 'quote', values: { text: 'hello' } }]);

    // 'hello' only reaches the DOM if the sub-field's own registered view component rendered it.
    expect(fixture.nativeElement.textContent).toContain('hello');
  });

  it('falls back to the stored block type name when the type is no longer configured', () => {
    const fixture = render(fieldValue(), [{ blockTypeName: 'removed', values: {} }]);

    expect(fixture.nativeElement.textContent).toContain('removed');
  });

  it('shows a dash for an empty or unreadable value', () => {
    expect(render(fieldValue(), []).nativeElement.textContent).toContain('-');
    expect(render(fieldValue(), undefined).nativeElement.textContent).toContain('-');
  });

  it('collapses to a block count in list mode', () => {
    const fixture = render(
      fieldValue(),
      [
        { blockTypeName: 'quote', values: { text: 'a' } },
        { blockTypeName: 'quote', values: { text: 'b' } },
      ],
      true,
    );

    expect(fixture.nativeElement.textContent.trim()).toBe('2 block(s)');
  });
});
