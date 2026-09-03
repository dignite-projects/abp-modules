import { Component, EventEmitter, Input, NgModule, Output, forwardRef } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * Test-only stand-in for `@ckeditor/ckeditor5-angular`'s `CKEditorComponent`
 * (`CKEditorControlComponent.spec.ts`'s `vi.mock('@ckeditor/ckeditor5-angular', ...)` returns this
 * module's `CKEditorModule` in place of the real one). The real `<ckeditor>` cannot mount in that spec:
 * its `ngAfterViewInit` calls `editor.create(...)` on whatever constructor its `[editor]` input holds,
 * and the spec's own `vi.mock('ckeditor5')` replaces every plugin class with a bare `{}`, which has no
 * `create()`. This stub only needs to exist, under the real `ckeditor` selector, and implement
 * `ControlValueAccessor` so `[formControlName]` on it still resolves.
 *
 * Deliberately its own file rather than declared inline inside the `vi.mock` factory: a factory that
 * dynamically `import()`s `@angular/core`/`@angular/forms` still resolves to the SAME hoisted binding
 * slot that spec file's own top-level imports of those packages claim (`Component`/`Input` are also
 * imported there directly), so referencing them from inside the factory throws a TDZ error at runtime
 * - vitest's own module-mocking hoisting only works cleanly when the factory has no such shared-module
 * entanglement with the file that declares it.
 */
@Component({
  // eslint-disable-next-line @angular-eslint/component-selector -- mocks the real @ckeditor/ckeditor5-angular <ckeditor> selector
  selector: 'ckeditor',
  // eslint-disable-next-line @angular-eslint/prefer-standalone -- mirrors the real package's NgModule-based CKEditorModule, see comment above
  standalone: false,
  template: '',
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => CkeditorAngularMockComponent), multi: true }],
})
export class CkeditorAngularMockComponent {
  @Input() editor: unknown;
  @Input() config: unknown;
  @Output() ready = new EventEmitter<unknown>();

  writeValue(): void {}
  registerOnChange(): void {}
  registerOnTouched(): void {}
}

// Angular's compiler flattens `CKEditorModule` (an NgModule import) into the exported directive it
// declares (`CKEditorComponent`, per the real package's own `ɵɵngDeclareNgModule` metadata) directly
// into `CKEditorControlComponent`'s compiled dependencies, which means the BUILD OUTPUT ends up with an
// implicit `import { CKEditorComponent } from '@ckeditor/ckeditor5-angular'` that the source file never
// wrote - this mock needs to satisfy that name too, not just `CKEditorModule`.
export { CkeditorAngularMockComponent as CKEditorComponent };

@NgModule({
  declarations: [CkeditorAngularMockComponent],
  exports: [CkeditorAngularMockComponent],
})
export class CKEditorModule {}
