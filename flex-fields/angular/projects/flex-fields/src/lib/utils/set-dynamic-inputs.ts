import { ComponentRef } from '@angular/core';

/**
 * Writes inputs on a component created via `ViewContainerRef.createComponent`, then marks it for
 * check — used by every one of this library's four dispatcher components
 * (`FlexFieldConfigComponent`, `FlexFieldControlComponent`, `FlexFieldSearchComponent`,
 * `FlexFieldViewComponent`) to hand the dynamically-created per-type component its inputs.
 *
 * `ComponentRef.setInput()` looks like the obvious tool for this and is Angular's documented way to
 * do it — it also marks the created component for check on our behalf, which matters because the
 * *dispatching* host (this library's own component) may itself be `OnPush`. But `setInput()` silently
 * no-ops (no error, no warning) for an `@Input()` that the target class only *inherits* — as a
 * get/set accessor pair — from a base class declared in a *different compilation unit* than the
 * target class itself. That is exactly this library's own shape: every per-type control/config/
 * search/view component extends an abstract base (`FieldTypeControlBase`, `FieldTypeConfigBase`)
 * declared here, but a *downstream* app's own field-type bolt-on (a `Tags` type, a custom one)
 * compiles its subclass in its own package. Angular's compiled `ɵcmp.inputs` metadata for such a
 * subclass correctly lists the inherited input — the bug is not in what gets declared, only in what
 * `setInput()` does with it — so nothing about this ever surfaces from this repo's own tests, whose
 * field types are all declared in the same compilation unit as the base classes.
 *
 * Direct property assignment always reaches an inherited setter — plain JS prototype-chain method
 * dispatch, unaffected by Ivy compilation boundaries — so inputs are spelled out here as property
 * writes, with the same "mark for check" call `setInput()` would otherwise have made for us.
 */
export function setDynamicInputs<T>(componentRef: ComponentRef<T>, inputs: Record<string, unknown>): void {
  const instance = componentRef.instance as Record<string, unknown>;
  for (const [name, value] of Object.entries(inputs)) {
    instance[name] = value;
  }
  componentRef.changeDetectorRef.markForCheck();
}
