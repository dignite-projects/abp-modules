import { DOCUMENT, Injectable, inject, signal } from '@angular/core';
import { CapturedDeclaration, applyDarkRewrite, restoreDarkRewrite } from './dark-mode-rewriter';

/**
 * Holds the demo's faked dark-mode state. Demo-only, never published - see `dark-mode-rewriter.ts`
 * for why the demo needs a faked dark host at all.
 *
 * Root-provided, and that is the whole point of it being a service rather than component state. The
 * rewrite mutates `document.styleSheets` directly, so the dark look survives Angular's client-side
 * navigation for free - switch it on, walk over to the products page, and the flex-field controls
 * are still being rendered against a dark host, which is exactly how it is meant to be used. The
 * list of overwritten declarations has to survive that navigation too. Were it component state, the
 * switch's own component would be destroyed on leaving its page and {@link captured} lost with it;
 * coming back would start from `isDark === false` over an already-dark document, rewrite it a second
 * time, and record the *dark* values as the originals - after which nothing could restore the light
 * ones short of a page reload.
 */
@Injectable({ providedIn: 'root' })
export class DevThemeService {
  private readonly document = inject(DOCUMENT);

  readonly isDark = signal(false);

  readonly rewrittenCount = signal(0);

  private captured: readonly CapturedDeclaration[] = [];

  toggle(): void {
    if (this.isDark()) {
      this.restore();
    } else {
      this.applyDark();
    }
  }

  /**
   * Sets both markers on `<html>`. `documentElement` rather than `body` matters more than it looks:
   * a custom property's `var()` references are resolved against the element the property is
   * *declared* on, not against wherever it is eventually consumed. Anything bridging host variables
   * into its own tokens from a `:root` block - `ckeditor-control.component.css` does exactly this -
   * can therefore only see variables set at `<html>`. Marking `<body>` instead leaves those `:root`
   * declarations resolving the light values while the rest of the page turns dark.
   */
  private applyDark(): void {
    const root = this.document.documentElement;
    root.setAttribute('data-bs-theme', 'dark');
    root.classList.add('lpx-theme-dark');
    this.captured = applyDarkRewrite(this.document);
    this.rewrittenCount.set(this.captured.length);
    this.isDark.set(true);
  }

  private restore(): void {
    const root = this.document.documentElement;
    root.removeAttribute('data-bs-theme');
    root.classList.remove('lpx-theme-dark');
    restoreDarkRewrite(this.captured);
    this.captured = [];
    this.rewrittenCount.set(0);
    this.isDark.set(false);
  }
}
