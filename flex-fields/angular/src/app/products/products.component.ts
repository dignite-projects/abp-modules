import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ABP, CoreModule, ListService, LIST_QUERY_DEBOUNCE_TIME, PagedResultDto, PermissionDirective } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import {
  FieldConfigurationDictionary,
  FlexFieldControlComponent,
  FlexFieldData,
  FlexFieldSearchComponent,
  FlexFieldValue,
  FlexFieldViewComponent,
} from '@dignite/ng.flex-fields';
import { FlexFieldQueryOperator, FlexFieldValueType } from '../proxy/dignite/abp/flex-fields';
import { ProductFieldService, ProductService } from '../proxy/services';
import {
  CreateUpdateProductDto,
  FlexFieldQueryConditionDto,
  ProductDto,
  ProductFieldDto,
} from '../proxy/services/dtos/models';

/**
 * Product catalogue: CRUD over `Product` plus filtering by flex field value - the demonstration this
 * whole demo exists for. `<ff-flex-field-view>` renders one column per field definition, `<ff-flex-field-search>`
 * builds the filter bar, and `<ff-flex-field-control>` builds the create/edit form - all three from the
 * same field definition list, none of it hand-written per field type.
 */
@Component({
  selector: 'app-products',
  templateUrl: './products.component.html',
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    PermissionDirective,
    FlexFieldControlComponent,
    FlexFieldSearchComponent,
    FlexFieldViewComponent,
  ],
  providers: [ListService, { provide: LIST_QUERY_DEBOUNCE_TIME, useValue: 300 }],
})
export class ProductsComponent {
  private readonly productService = inject(ProductService);
  private readonly fieldService = inject(ProductFieldService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  readonly list = inject(ListService);

  data: PagedResultDto<ProductDto> = { items: [], totalCount: 0 };

  /** Every field definition - drives the table's dynamic columns and the create/edit form. */
  fields: ProductFieldDto[] = [];

  /** The subset with `searchable: true` - drives the filter bar. Excludes `description` (a free-text
   *  field not worth filtering on in this demo) and never includes DateTime, Matrix or Table fields,
   *  since none of those ship a search component in the library. */
  get searchableFields(): ProductFieldDto[] {
    return this.fields.filter(f => f.searchable);
  }

  readonly searchForm = this.fb.group({ flexFieldSearch: this.fb.group({}) });
  private appliedConditions: FlexFieldQueryConditionDto[] = [];

  isModalOpen = false;
  isModalBusy = false;
  editingProduct?: ProductDto;
  form?: FormGroup;

  @ViewChild('submitButton') submitButton?: ElementRef<HTMLButtonElement>;

  /**
   * Stable per-field definition value (no per-row value), for the search bar and the create/edit
   * form. Keyed by field name and rebuilt only when the field list itself changes.
   */
  private definitionValues = new Map<string, FlexFieldValue>();

  /**
   * Stable per-row field value for the list, keyed by product id then field name. Rebuilt only when
   * a new page of data arrives - never on every change-detection pass.
   *
   * Both caches exist for one reason: `<ff-flex-field-*>` decide whether to re-render by comparing
   * the `fields` input's *object reference* (`OnChanges`). Binding `[fields]="someMethodCall(...)"`
   * directly in a template calls that method - and allocates a new object - on every CD run. The
   * child sees a "changed" input, tears down and recreates its dynamic sub-component, which is
   * itself a CD-triggering operation - Angular flags that as `NG0103: Infinite change detection` and
   * the page never finishes rendering. Reading a cached, stable reference instead means the input
   * only actually changes when the underlying data does.
   */
  private rowValues = new Map<string, Map<string, FlexFieldValue>>();

  constructor() {
    this.fieldService.getList({ maxResultCount: 1000 }).subscribe(result => {
      this.fields = result.items;
      this.definitionValues = new Map(this.fields.map(f => [f.name!, this.toFlexFieldValue(f)] as const));
      this.rebuildRowValues();
    });

    const getData = (query: ABP.PageQueryParams) =>
      this.productService.search({
        skipCount: query.skipCount,
        maxResultCount: query.maxResultCount ?? this.list.maxResultCount,
        flexFieldConditions: this.appliedConditions,
      });
    const setData = (result: PagedResultDto<ProductDto>) => {
      this.data = result;
      this.rebuildRowValues();
    };
    this.list.hookToQuery(getData).subscribe(setData);
  }

  /** The stable `FlexFieldValue` for the search bar / create-edit form. See {@link definitionValues}. */
  definitionValueOf(field: ProductFieldDto): FlexFieldValue {
    const name = field.name!;
    const existing = this.definitionValues.get(name);
    if (existing) {
      return existing;
    }

    const value = this.toFlexFieldValue(field);
    this.definitionValues.set(name, value);
    return value;
  }

  /** The stable `FlexFieldValue` for one list cell. See {@link rowValues}. */
  rowValueOf(product: ProductDto, field: ProductFieldDto): FlexFieldValue {
    const productValues = this.rowValues.get(product.id!);
    const name = field.name!;
    const existing = productValues?.get(name);
    if (existing) {
      return existing;
    }

    const value = this.toFlexFieldValue(field, this.rawValueOf(product, field));
    if (productValues) {
      productValues.set(name, value);
    } else {
      this.rowValues.set(product.id!, new Map([[name, value]]));
    }
    return value;
  }

  /** Rebuilds the per-row cache whenever either the product page or its field definitions arrive. */
  private rebuildRowValues(): void {
    this.rowValues = new Map(
      this.data.items.map(
        product =>
          [
            product.id!,
            new Map(
              this.fields.map(f => [f.name!, this.toFlexFieldValue(f, this.rawValueOf(product, f))] as const),
            ),
          ] as const,
      ),
    );
  }

  /** `<ff-flex-field-*>` components take a definition + usage flags + value, not a raw DTO - this bridges. */
  private toFlexFieldValue(field: ProductFieldDto, value?: unknown): FlexFieldValue {
    return {
      field: this.toFieldData(field),
      required: !!field.required,
      searchable: !!field.searchable,
      value,
    };
  }

  private toFieldData(field: ProductFieldDto): FlexFieldData {
    return {
      id: field.id!,
      name: field.name!,
      displayName: field.displayName!,
      description: field.description ?? undefined,
      fieldTypeName: field.fieldTypeName!,
      configuration: (field.configuration ?? {}) as FieldConfigurationDictionary,
    };
  }

  /** A product's current value for one field, or undefined if it was never set. */
  private rawValueOf(product: ProductDto, field: ProductFieldDto): unknown {
    return product.flexFieldValues?.find(v => v.field?.name === field.name)?.value;
  }

  // --- search -----------------------------------------------------------------------------------

  applySearch(): void {
    this.appliedConditions = this.buildConditions();
    this.list.get();
  }

  /**
   * Resets every existing control's value rather than replacing the `flexFieldSearch` group itself.
   * Each `<ff-flex-field-search>` instance keeps its own reference to the group it registered its
   * control on (`FieldTypeControlBase.valuesGroup`), captured once when its `entity`/`parentFieldName`
   * inputs were first set. Swapping in a brand-new group here would not change either of those
   * inputs' object identity from the child's point of view (`searchForm` itself is unchanged), so no
   * child would ever notice and re-register - every one of them would go on writing into the old,
   * now-detached group forever, and `buildConditions()` would read the new, permanently empty one.
   */
  clearSearch(): void {
    const group = this.searchForm.get('flexFieldSearch') as FormGroup;
    Object.values(group.controls).forEach(control => control.reset(''));
    this.appliedConditions = [];
    this.list.get();
  }

  /**
   * Reads the raw value each `<ff-flex-field-search>` wrote into `searchForm.flexFieldSearch` and turns
   * it into the `FlexFieldQueryCondition`s the backend understands. The mapping depends on the field
   * type's storage shape, not on anything the search form knows about generically:
   * - `Text`: substring match.
   * - `Number`: `ff-number-search` writes a single `"min-max"` string; split into two AND'ed bounds
   *   (GreaterThanOrEqual + LessThanOrEqual on the same field - the executor composes multiple
   *   conditions on one field as a range, not a conflict).
   * - `Select` / `Tree`: multi-select, so `In` over the comma-joined selection.
   * - `Boolean`: the native `<select>` in `ff-boolean-search` yields the *strings* `"true"`/`"false"`,
   *   not booleans - a plain `<option [value]>` binding always stringifies.
   * - `DateTime`, `Matrix`, `Table`: no search component exists for any of them, so they never
   *   contribute a condition. For the two composites that is permanent - the server's
   *   `IndexValueType` is null, so their values never reach the query index at all.
   */
  private buildConditions(): FlexFieldQueryConditionDto[] {
    const raw = (this.searchForm.get('flexFieldSearch')?.value ?? {}) as Record<string, unknown>;
    const conditions: FlexFieldQueryConditionDto[] = [];

    for (const field of this.searchableFields) {
      conditions.push(...this.toConditions(field, raw[field.name!]));
    }

    return conditions;
  }

  private toConditions(field: ProductFieldDto, value: unknown): FlexFieldQueryConditionDto[] {
    switch (field.fieldTypeName) {
      case 'Text':
        return this.isBlank(value)
          ? []
          : [this.condition(field, FlexFieldQueryOperator.Contains, String(value), FlexFieldValueType.String)];

      case 'Number': {
        const range = typeof value === 'string' ? value.split('-') : [];
        if (range.length !== 2 || range.some(part => part === '')) {
          return [];
        }
        return [
          this.condition(field, FlexFieldQueryOperator.GreaterThanOrEqual, range[0], FlexFieldValueType.Number),
          this.condition(field, FlexFieldQueryOperator.LessThanOrEqual, range[1], FlexFieldValueType.Number),
        ];
      }

      case 'Boolean':
        return value === 'true' || value === 'false'
          ? [this.condition(field, FlexFieldQueryOperator.Equals, value, FlexFieldValueType.Boolean)]
          : [];

      case 'Select':
      case 'Tree': {
        const values = Array.isArray(value) ? value : this.isBlank(value) ? [] : [value];
        return values.length === 0
          ? []
          : [this.condition(field, FlexFieldQueryOperator.In, values.join(','), FlexFieldValueType.String)];
      }

      default:
        return [];
    }
  }

  private condition(
    field: ProductFieldDto,
    operator: FlexFieldQueryOperator,
    value: string,
    valueType: FlexFieldValueType,
  ): FlexFieldQueryConditionDto {
    return { fieldId: field.id!, fieldName: field.name!, operator, value, valueType };
  }

  private isBlank(value: unknown): boolean {
    return value === null || value === undefined || value === '';
  }

  // --- create / edit ------------------------------------------------------------------------------

  openCreate(): void {
    this.form = this.fb.group({
      name: [''],
      flexFields: this.fb.group({}),
    });
    this.editingProduct = undefined;
    this.isModalOpen = true;
  }

  openEdit(product: ProductDto): void {
    this.form = this.fb.group({
      name: [product.name],
      flexFields: this.fb.group({}),
    });
    this.editingProduct = product;
    this.isModalOpen = true;
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.form = undefined;
    this.editingProduct = undefined;
  }

  save(): void {
    if (!this.form || this.form.invalid || this.isModalBusy) {
      return;
    }

    this.isModalBusy = true;
    const value = this.form.getRawValue();
    const input: CreateUpdateProductDto = {
      name: value.name,
      flexFields: (this.form.get('flexFields')?.getRawValue() ?? {}) as Record<string, object>,
    };

    const request$ = this.editingProduct
      ? this.productService.update(this.editingProduct.id!, input)
      : this.productService.create(input);

    request$.subscribe({
      next: () => {
        this.isModalBusy = false;
        this.closeModal();
        this.list.get();
        this.toaster.success('Demo::SavedSuccessfully');
      },
      error: () => (this.isModalBusy = false),
    });
  }

  delete(product: ProductDto): void {
    this.confirmation
      .warn('Demo::ProductDeletionConfirmationMessage', 'AbpUi::AreYouSure')
      .subscribe((status: Confirmation.Status) => {
        if (status === Confirmation.Status.confirm) {
          this.productService.delete(product.id!).subscribe(() => {
            this.list.get();
            this.toaster.success('Demo::DeletedSuccessfully');
          });
        }
      });
  }
}
