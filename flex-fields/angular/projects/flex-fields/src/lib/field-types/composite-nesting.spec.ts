import { TestBed } from '@angular/core/testing';
import {
  MAX_COMPOSITE_NESTING_DEPTH,
  allowsCompositeAt,
  nextCompositeNestingDepth,
} from './composite-nesting';

describe('allowsCompositeAt', () => {
  it('leaves room for a composite at the first two levels but not the third', () => {
    // MaxDepth 3: a top-level field may be composite (1), so may its sub-fields (2), but what those
    // declare (3) has to be scalar. Mirrors CompositeFieldNesting.MaxDepth on the server.
    expect(MAX_COMPOSITE_NESTING_DEPTH).toBe(3);
    expect(allowsCompositeAt(1)).toBe(true);
    expect(allowsCompositeAt(2)).toBe(true);
    expect(allowsCompositeAt(3)).toBe(false);
  });
});

describe('nextCompositeNestingDepth', () => {
  it("puts a top-level field type's own sub-fields at depth 2", () => {
    const depth = TestBed.runInInjectionContext(() => nextCompositeNestingDepth());
    expect(depth).toBe(2);
  });

  // The "one deeper than whatever it was mounted inside" half needs a real node-injector chain to
  // exercise `skipSelf`, so it is asserted where it actually matters: the config components'
  // `fieldTypeOptions`, which stops offering composite types once the depth runs out. See
  // `matrix-config.component.spec.ts` / `table-config.component.spec.ts`.
});
