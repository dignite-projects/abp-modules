import { InjectionToken, inject } from '@angular/core';

/**
 * How many levels of field definition a field may span, counting the field itself as 1. At the current
 * value of `3`: a top-level field may be composite, and so may its columns/sub-fields (one layer of
 * nesting) - but what *that* nested composite declares must be scalar.
 *
 * Mirrors `CompositeFieldNesting.MaxDepth`
 * (`src/Dignite.Abp.FlexFields.Abstractions/Dignite/Abp/FlexFields/CompositeFieldNesting.cs`), which is
 * the authority - a host that enforces it (the demo's `ProductFieldAppService` does) refuses a
 * configuration that exceeds it whatever this file says. This copy exists so the designer can stop
 * *offering* the choice instead of letting the admin build something the save will reject.
 */
export const MAX_COMPOSITE_NESTING_DEPTH = 3;

/**
 * The depth at which the *sub-fields* of the config editor currently being rendered live. Absent at the
 * top level, where a field is depth 1 and its own sub-fields would be depth 2.
 */
export const COMPOSITE_NESTING_DEPTH = new InjectionToken<number>('COMPOSITE_NESTING_DEPTH');

/**
 * Factory for a composite config component's own `COMPOSITE_NESTING_DEPTH` provider: one deeper than
 * whatever it was mounted inside.
 *
 * `ff-flex-field-config` mounts a nested config editor with `ViewContainerRef.createComponent`, which
 * uses the host element's injector - so the chain of composite components on screen *is* the injector
 * chain, and `skipSelf` walks it. Nothing has to thread a depth through the library's inputs (which is
 * just as well: it has none for it).
 */
export function nextCompositeNestingDepth(): number {
  return (inject(COMPOSITE_NESTING_DEPTH, { optional: true, skipSelf: true }) ?? 1) + 1;
}

/**
 * Whether a field at `depth` may itself be composite - i.e. whether there is room left underneath it
 * for the fields its own configuration would declare.
 */
export function allowsCompositeAt(depth: number): boolean {
  return depth < MAX_COMPOSITE_NESTING_DEPTH;
}
