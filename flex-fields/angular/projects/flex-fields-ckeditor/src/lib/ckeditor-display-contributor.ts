import { InjectionToken } from '@angular/core';
import type { FlexFieldData } from '@dignite/ng.flex-fields';
import { CKEditorContentFormat } from './ckeditor-content-format';

/** What a {@link CKEditorDisplayContributor} is told about the value it is rewriting. */
export interface CKEditorDisplayContext {
  /** The field being displayed - its `name` and its `configuration` (`CKEditor.*` keys). */
  readonly field: FlexFieldData;

  /** The format the field stores. The `html` a contributor gets is HTML either way (Markdown is converted first). */
  readonly contentFormat: CKEditorContentFormat;
}

/**
 * Rewrites the HTML the read-only view (`ff-ckeditor-view`) is about to display, and returns the HTML to
 * display instead - for example to turn the relative file addresses a field stores into absolute ones.
 *
 * Runs when the view's value or field changes, after a Markdown value has been converted to HTML and before the
 * result is bound to `[innerHTML]`, so Angular's own sanitizer still has the last word on what reaches
 * the DOM. Only the displayed HTML changes; the stored value does not. Skipped when the view was given no
 * field to describe (no `fields` input), since there is then no {@link CKEditorDisplayContext} to pass. Registered under
 * {@link CKEDITOR_DISPLAY_CONTRIBUTORS}.
 */
export type CKEditorDisplayContributor = (html: string, context: CKEditorDisplayContext) => string;

/**
 * The host's {@link CKEditorDisplayContributor}s, applied in registration order - each one gets the HTML
 * the previous one returned. A multi provider, the display-side counterpart of
 * `CKEDITOR_CONFIG_CONTRIBUTORS`:
 *
 * ```ts
 * providers: [
 *   provideFlexFields(),
 *   provideCKEditorFieldType(),
 *   { provide: CKEDITOR_DISPLAY_CONTRIBUTORS, multi: true, useValue: myContributor },
 *   // or, for a contributor that needs services:
 *   { provide: CKEDITOR_DISPLAY_CONTRIBUTORS, multi: true, useFactory: myContributorFactory },
 * ]
 * ```
 *
 * With none registered, the view displays the HTML unchanged.
 */
export const CKEDITOR_DISPLAY_CONTRIBUTORS = new InjectionToken<readonly CKEditorDisplayContributor[]>(
  'CKEDITOR_DISPLAY_CONTRIBUTORS',
);

/** `html` passed through each of `contributors` in order; `html` itself when there are none. */
export function applyDisplayContributors(
  html: string,
  contributors: readonly CKEditorDisplayContributor[],
  context: CKEditorDisplayContext,
): string {
  let current = html;
  for (const contribute of contributors) {
    current = contribute(current, context);
  }

  return current;
}
