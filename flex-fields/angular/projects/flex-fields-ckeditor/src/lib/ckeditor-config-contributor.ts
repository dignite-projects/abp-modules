import { InjectionToken } from '@angular/core';
import type { EditorConfig } from 'ckeditor5';
import type { FlexFieldData } from '@dignite/ng.flex-fields';
import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorMode } from './ckeditor-mode';

/** What a {@link CKEditorConfigContributor} is told about the editor it is configuring. */
export interface CKEditorConfigContext {
  /**
   * The `ckeditor5` package, already loaded. Take a built-in plugin from here (`ckeditor5.Alignment`)
   * rather than with a static `import { Alignment } from 'ckeditor5'`: a static import pulls the whole
   * multi-megabyte package into the host's main bundle, out of the lazy chunk this package loads it in.
   * Type-only imports (`import type { Editor } from 'ckeditor5'`) are safe.
   */
  readonly ckeditor5: typeof import('ckeditor5');

  /** The field being edited - its `name` and its `configuration` (`CKEditor.*` keys). */
  readonly field: FlexFieldData;

  readonly mode: CKEditorMode;

  readonly contentFormat: CKEditorContentFormat;
}

/**
 * Changes the configuration a `CKEditor` field's editor is created with - add a plugin to
 * `extraPlugins`, change a toolbar, set any other `EditorConfig` option. Either change `config` in place
 * and return nothing, or return the configuration to use instead.
 *
 * Runs once per editor, when the editor is created, after this package has composed its own
 * configuration (`buildEditorConfig`). Registered under {@link CKEDITOR_CONFIG_CONTRIBUTORS}.
 */
export type CKEditorConfigContributor = (
  config: EditorConfig,
  context: CKEditorConfigContext,
) => EditorConfig | void;

/**
 * The host's {@link CKEditorConfigContributor}s, applied in registration order - each one gets the
 * configuration the previous one left. A multi provider, like `CUSTOM_ERROR_HANDLERS` in
 * `@abp/ng.theme.shared`, so any number of libraries and the application can each register their own:
 *
 * ```ts
 * providers: [
 *   provideFlexFields(),
 *   provideCKEditorFieldType(),
 *   { provide: CKEDITOR_CONFIG_CONTRIBUTORS, multi: true, useValue: myContributor },
 *   // or, for a contributor that needs services:
 *   { provide: CKEDITOR_CONFIG_CONTRIBUTORS, multi: true, useFactory: myContributorFactory },
 * ]
 * ```
 *
 * With none registered, every editor gets this package's own configuration unchanged.
 */
export const CKEDITOR_CONFIG_CONTRIBUTORS = new InjectionToken<readonly CKEditorConfigContributor[]>(
  'CKEDITOR_CONFIG_CONTRIBUTORS',
);

/** `config` passed through each of `contributors` in order; `config` itself when there are none. */
export function applyEditorConfigContributors(
  config: EditorConfig,
  contributors: readonly CKEditorConfigContributor[],
  context: CKEditorConfigContext,
): EditorConfig {
  let current = config;
  for (const contribute of contributors) {
    const returned = contribute(current, context);
    if (returned) {
      current = returned;
    }
  }

  return current;
}
