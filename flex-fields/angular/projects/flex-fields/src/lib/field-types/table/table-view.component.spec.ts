import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { FlexFieldValue } from '../../models';
import { provideFlexFields } from '../../providers';
import { TableViewComponent } from './table-view.component';

const COLUMNS = [
  { name: 'title', displayName: 'Title', fieldTypeName: 'Text', required: false, configuration: {} },
  { name: 'qty', displayName: 'Quantity', fieldTypeName: 'Number', required: false, configuration: {} },
];

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'specs',
      displayName: 'Specs',
      fieldTypeName: 'Table',
      configuration: { 'Table.Columns': COLUMNS },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function render(fields: FlexFieldValue, value: unknown, showInList = false) {
  const fixture = TestBed.createComponent(TableViewComponent);
  fixture.componentRef.setInput('fields', fields);
  fixture.componentRef.setInput('type', 'Table');
  fixture.componentRef.setInput('value', value);
  fixture.componentRef.setInput('showInList', showInList);
  fixture.detectChanges();
  return fixture;
}

describe('TableViewComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
      providers: [provideFlexFields()],
    });
  });

  it('renders one header per configured column', () => {
    const fixture = render(fieldValue(), [{ values: { title: 'One', qty: 2 } }]);

    const headers = [...fixture.nativeElement.querySelectorAll('thead th')].map((th: HTMLElement) =>
      th.textContent!.trim(),
    );
    expect(headers).toEqual(['Title', 'Quantity']);
  });

  it('recurses into <ff-flex-field-view> for each cell', () => {
    const fixture = render(fieldValue(), [{ values: { title: 'One', qty: 2 } }]);

    const cells = [...fixture.nativeElement.querySelectorAll('tbody td')].map((td: HTMLElement) =>
      td.textContent!.trim(),
    );
    // Values only reach the DOM if each column's own registered view component rendered them.
    expect(cells).toEqual(['One', '2']);
  });

  it('shows a dash when there are no rows, no columns, or nothing readable', () => {
    expect(render(fieldValue(), []).nativeElement.textContent).toContain('-');
    expect(render(fieldValue(), undefined).nativeElement.textContent).toContain('-');
    expect(
      render(
        fieldValue({
          field: {
            id: '1',
            name: 'specs',
            displayName: 'Specs',
            fieldTypeName: 'Table',
            configuration: {},
          },
        }),
        [{ values: { title: 'One' } }],
      ).nativeElement.textContent,
    ).toContain('-');
  });

  it('collapses to a row count in list mode', () => {
    const fixture = render(
      fieldValue(),
      [{ values: { title: 'One', qty: 1 } }, { values: { title: 'Two', qty: 2 } }],
      true,
    );

    expect(fixture.nativeElement.textContent.trim()).toBe('2 row(s)');
  });
});
