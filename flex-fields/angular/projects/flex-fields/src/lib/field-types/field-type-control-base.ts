import {
  ChangeDetectorRef,
  Directive,
  ElementRef,
  Input,
  OnDestroy,
  ViewChild,
  inject,
} from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup } from '@angular/forms';
import { FieldConfigurationDictionary, FlexFieldValue } from '../models';

/**
 * Shared plumbing for every field type's **control** and **search** component: bind the field to a
 * named control inside the host form's value-bag group, and remove it again on destroy.
 *
 * Subclasses supply only what differs — {@link createControl} (which validators, and whether the
 * value is a single control or a group) and {@link configurationDefaults}.
 *
 * `@Directive()` with no selector is what lets an abstract base declare `@Input()`s; without it
 * Angular rejects the subclass for "using Angular features but is not decorated".
 */
@Directive()
export abstract class FieldTypeControlBase implements OnDestroy {
  protected readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  /** The whole host form. */
  protected entityForm?: FormGroup;
  @Input()
  set entity(value: FormGroup) {
    this.entityForm = value;
    this.rebuild();
  }

  /** Definition + per-usage flags + current value for the field being rendered. */
  protected fieldValue?: FlexFieldValue;
  @Input()
  set fields(value: FlexFieldValue) {
    this.fieldValue = value;
    this.rebuild();
  }

  /** Name of the group inside the host form that holds flex field values. */
  protected parentField?: string;
  @Input()
  set parentFieldName(value: string) {
    this.parentField = value;
    this.rebuild();
  }

  /** The value to seed the control with. */
  protected selectedValue: unknown = '';
  @Input()
  set selected(value: unknown) {
    this.selectedValue = value ?? '';
    this.rebuild();
  }

  /** The group inside the host form that holds flex field values. */
  protected valuesGroup?: FormGroup;

  /**
   * This field's control inside {@link valuesGroup}.
   *
   * The array form of `get` matters: passing a string makes Angular treat `.` as a path separator, and
   * a field named `price.net` would then be looked up as a nested group called `price`. Nothing stops
   * a field name from containing a dot.
   */
  protected get fieldControl(): AbstractControl | null {
    return this.fieldValue ? (this.valuesGroup?.get([this.fieldValue.field.name]) ?? null) : null;
  }

  // Clicked once the control exists, so the host form runs its validators and any error the field
  // already has is shown rather than waiting for the user to touch it.
  @ViewChild('submitclick', { static: true }) submitClick?: ElementRef<HTMLButtonElement>;

  /** Seed values for this field type's configuration keys, as a `FormBuilder.group()` shape. */
  protected abstract configurationDefaults(): object;

  /** The control this field type binds — usually a single control, a group for range filters. */
  protected abstract createControl(): AbstractControl;

  private rebuild(): void {
    if (!this.fieldValue || !this.entityForm || !this.parentField) {
      return;
    }

    this.valuesGroup = this.entityForm.get(this.parentField) as FormGroup;
    this.applyConfigurationDefaults();
    this.valuesGroup?.setControl(this.fieldValue.field.name, this.createControl());

    this.cdr.detectChanges();
    this.submitClick?.nativeElement?.click();
  }

  /**
   * Fills in any configuration key the stored dictionary is missing. A field configured before a key
   * existed has no value for it, and a template reading `configuration['TextEdit.Mode']` off that
   * would get `undefined` rather than the default the server would have applied.
   */
  private applyConfigurationDefaults(): void {
    const field = this.fieldValue!.field;
    const defaults = this.fb.group(this.configurationDefaults()).value as FieldConfigurationDictionary;

    field.configuration = { ...defaults, ...field.configuration };
  }

  ngOnDestroy(): void {
    if (this.fieldValue) {
      this.valuesGroup?.removeControl(this.fieldValue.field.name);
    }
  }
}
