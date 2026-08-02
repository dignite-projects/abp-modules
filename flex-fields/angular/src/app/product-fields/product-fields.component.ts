import { Component, ViewChild, ElementRef, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  ABP,
  CoreModule,
  ListService,
  LIST_QUERY_DEBOUNCE_TIME,
  PagedResultDto,
  PermissionDirective,
} from '@abp/ng.core';
import { Confirmation, ConfirmationService, ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import { NgxDatatableModule } from '@swimlane/ngx-datatable';
import { FieldTypeDefinition, FieldTypeResolver, FlexFieldConfigComponent } from '@dignite/ng.flex-fields';
import { ProductFieldService } from '../proxy/services';
import {
  CreateProductFieldDto,
  ProductFieldDto,
  UpdateProductFieldDto,
} from '../proxy/services/dtos/models';

/**
 * Field-definition management: create, edit and delete the ProductField definitions Product uses. The
 * edit dialog embeds <ff-flex-field-config>, which swaps in the right configuration editor for whichever
 * field type is selected - this page is that component's demonstration site.
 */
@Component({
  selector: 'app-product-fields',
  templateUrl: './product-fields.component.html',
  imports: [
    CoreModule,
    ThemeSharedModule,
    ReactiveFormsModule,
    NgxDatatableModule,
    PermissionDirective,
    FlexFieldConfigComponent,
  ],
  providers: [ListService, { provide: LIST_QUERY_DEBOUNCE_TIME, useValue: 300 }],
})
export class ProductFieldsComponent {
  private readonly fieldService = inject(ProductFieldService);
  private readonly fieldTypeResolver = inject(FieldTypeResolver);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  readonly list = inject(ListService);

  data: PagedResultDto<ProductFieldDto> = { items: [], totalCount: 0 };

  readonly fieldTypes: readonly FieldTypeDefinition[] = this.fieldTypeResolver.getAll();

  isModalOpen = false;
  isModalBusy = false;
  editingField?: ProductFieldDto;
  form?: FormGroup;

  /**
   * Whether the currently selected field type rules out searching. Drives the disabled state of the
   * "searchable" checkbox and the hint that explains it.
   */
  searchableUnsupported = false;

  /**
   * Which field types can be searched at all, keyed by registration name - straight from the server, via
   * ProductFieldAppService.GetFieldTypesAsync. It has to come from there: the answer is
   * IFieldType.IndexValueType, and the Angular library deliberately does not restate it (see
   * FieldTypeDefinition's doc). Empty until the request lands, which reads as "no restriction" - the
   * server rejects the combination anyway, so a slow response can at worst let a save fail loudly.
   */
  private indexableByFieldType = new Map<string, boolean>();

  @ViewChild('submitButton') submitButton?: ElementRef<HTMLButtonElement>;

  constructor() {
    // The backend always sorts by Name (GetProductFieldListDto has no Sorting input), so only
    // paging (skipCount/maxResultCount) from the query actually matters here. maxResultCount is
    // optional on ABP.PageQueryParams but required on the generated DTO, hence the fallback.
    const getData = (query: ABP.PageQueryParams) =>
      this.fieldService.getList({
        skipCount: query.skipCount,
        maxResultCount: query.maxResultCount ?? this.list.maxResultCount,
      });
    const setData = (result: PagedResultDto<ProductFieldDto>) => (this.data = result);
    this.list.hookToQuery(getData).subscribe(setData);

    this.fieldService.getFieldTypes().subscribe(fieldTypes => {
      this.indexableByFieldType = new Map(
        fieldTypes.map(fieldType => [fieldType.name ?? '', fieldType.indexable ?? true]),
      );
      // A modal opened before this landed was built against an empty map - re-apply so it catches up.
      this.applySearchableAvailability(this.form?.getRawValue().fieldTypeName);
    });
  }

  /** Localization key for a field type's display name, e.g. "FlexFields::FieldType:Text" - use with | abpLocalization. */
  displayNameKeyOf(fieldTypeName: string): string {
    return this.fieldTypes.find(t => t.name === fieldTypeName)?.displayNameKey ?? fieldTypeName;
  }

  openCreate(): void {
    this.editingField = undefined;
    this.isModalOpen = true;
    const fieldTypeName = this.fieldTypes[0]?.name ?? '';
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(64)]],
      displayName: ['', [Validators.required, Validators.maxLength(128)]],
      description: [''],
      fieldTypeName: [fieldTypeName, Validators.required],
      required: [false],
      searchable: [false],
    });
    this.applySearchableAvailability(fieldTypeName);
  }

  openEdit(field: ProductFieldDto): void {
    this.editingField = field;
    this.isModalOpen = true;
    this.form = this.fb.group({
      name: [field.name, [Validators.required, Validators.maxLength(64)]],
      displayName: [field.displayName, [Validators.required, Validators.maxLength(128)]],
      description: [field.description],
      // Fixed once created - ff-flex-field-config still needs it to pick which editor to render.
      fieldTypeName: [{ value: field.fieldTypeName, disabled: true }],
      required: [field.required],
      searchable: [field.searchable],
    });
    this.applySearchableAvailability(field.fieldTypeName);
  }

  /**
   * Re-applied whenever the create form's field-type dropdown changes (the edit form's is fixed, so
   * openEdit only needs the one-time call above).
   */
  onFieldTypeChange(event: Event): void {
    this.applySearchableAvailability((event.target as HTMLSelectElement).value);
  }

  /**
   * A field type with no query-index slot (FileExplorer) can never actually be searched -
   * FlexFieldIndexManagerBase.GetIndexableFieldsAsync skips it regardless of `Searchable`. Lock the
   * checkbox off rather than let an admin set a flag that looks like it did something; the hint beside
   * it says why, since a control that is merely greyed out reads as broken.
   */
  private applySearchableAvailability(fieldTypeName?: string): void {
    this.searchableUnsupported =
      !!fieldTypeName && this.indexableByFieldType.get(fieldTypeName) === false;

    const searchableControl = this.form?.get('searchable');
    if (!searchableControl) {
      return;
    }

    if (this.searchableUnsupported) {
      searchableControl.setValue(false);
      searchableControl.disable();
    } else {
      searchableControl.enable();
    }
  }

  closeModal(): void {
    this.isModalOpen = false;
    this.form = undefined;
    this.editingField = undefined;
  }

  save(): void {
    if (!this.form || this.form.invalid || this.isModalBusy) {
      return;
    }

    this.isModalBusy = true;
    const value = this.form.getRawValue();
    const configuration = this.form.get('configuration')?.value ?? {};

    const request$ = this.editingField
      ? this.fieldService.update(this.editingField.id!, {
          name: value.name,
          displayName: value.displayName,
          description: value.description,
          configuration,
          required: value.required,
          searchable: value.searchable,
        } satisfies UpdateProductFieldDto)
      : this.fieldService.create({
          name: value.name,
          displayName: value.displayName,
          description: value.description,
          fieldTypeName: value.fieldTypeName,
          configuration,
          required: value.required,
          searchable: value.searchable,
        } satisfies CreateProductFieldDto);

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

  delete(field: ProductFieldDto): void {
    this.confirmation.warn('Demo::FieldDeletionConfirmationMessage', 'AbpUi::AreYouSure').subscribe((status: Confirmation.Status) => {
      if (status === Confirmation.Status.confirm) {
        this.fieldService.delete(field.id!).subscribe(() => {
          this.list.get();
          this.toaster.success('Demo::DeletedSuccessfully');
        });
      }
    });
  }
}
