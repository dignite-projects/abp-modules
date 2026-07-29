import { Injectable, inject } from '@angular/core';
import { FieldTypeDefinition } from './field-type-definition';
import { FLEX_FIELD_TYPES } from './field-type.tokens';

/**
 * Looks a field type up by its registration key. Mirrors `IFieldTypeResolver` on the server.
 *
 * Registrations are flattened in provider order and **later wins**, so an application can override a
 * built-in by registering its own definition under the same name after
 * {@link provideFlexFields} has contributed the built-ins.
 */
@Injectable({ providedIn: 'root' })
export class FieldTypeResolver {
  private readonly fieldTypes: ReadonlyMap<string, FieldTypeDefinition> = new Map(
    (inject(FLEX_FIELD_TYPES, { optional: true }) ?? [])
      .flat()
      .map(fieldType => [fieldType.name, fieldType] as const),
  );

  /**
   * Every registered field type, in registration order. Use this to build a "which type is this
   * field?" picker in a field designer.
   */
  getAll(): readonly FieldTypeDefinition[] {
    return [...this.fieldTypes.values()];
  }

  /** The field type registered under `fieldTypeName`, or `undefined` when none is. */
  find(fieldTypeName: string): FieldTypeDefinition | undefined {
    return this.fieldTypes.get(fieldTypeName);
  }

  /**
   * The field type registered under `fieldTypeName`. Throws when none is — a field bound to an
   * unregistered type is a wiring bug (a missing `provideFlexFields()`, or a bolt-on package the host
   * forgot to register), and rendering nothing would hide it until someone noticed the blank form.
   */
  get(fieldTypeName: string): FieldTypeDefinition {
    const fieldType = this.fieldTypes.get(fieldTypeName);

    if (!fieldType) {
      throw new Error(
        `No flex field type is registered under the name '${fieldTypeName}'. ` +
          `Registered: ${[...this.fieldTypes.keys()].join(', ') || '(none)'}. ` +
          'Did you call provideFlexFields() in your application config?',
      );
    }

    return fieldType;
  }
}
