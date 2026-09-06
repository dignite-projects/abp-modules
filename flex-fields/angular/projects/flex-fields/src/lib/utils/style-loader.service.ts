import { Injectable, InjectionToken, inject } from '@angular/core';
import { LOADING_STRATEGY, LazyLoadService } from '@abp/ng.core';

/**
 * One third-party global stylesheet, described the way a host declares it.
 *
 * The two halves are the two halves of an `angular.json` `styles` entry, so the error this service
 * logs when the file is missing can quote the exact entry to add rather than describe it.
 */
export interface StyleBundle {
  /** `bundleName` of the host's `styles` entry. The file is served as `<bundleName>.css`. */
  readonly bundleName: string;
  /** `input` of that same entry, relative to the host's workspace root. */
  readonly input: string;
}

/** `<nz-select>`'s stylesheet — loaded by the `Select` field type's control and search components. */
export const NZ_SELECT_STYLE: StyleBundle = {
  bundleName: 'ng-zorro-antd-select',
  input: 'node_modules/ng-zorro-antd/select/style/index.min.css',
};

/**
 * Provide `true` to stop this package family from lazy-loading any third-party stylesheet.
 *
 * For an application that already gets that CSS some other way — an `inject: true` entry, a global
 * `.less` build, a CDN `<link>` in `index.html` — and would otherwise fetch it twice:
 *
 * ```ts
 * providers: [{ provide: DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN, useValue: true }]
 * ```
 *
 * It is family-wide and all-or-nothing: `true` silences every bundle loaded through
 * {@link FlexFieldsStyleLoader}, in every `@dignite/ng.flex-fields*` package, not one bundle at a
 * time. An application that wants one of them and not the others has to declare the entries it wants
 * and leave this token unset.
 *
 * The counterpart for the `Tree` field type is ABP's own `DISABLE_TREE_STYLE_LOADING_TOKEN`
 * (`@abp/ng.components/tree`): `abp-tree` loads `ng-zorro-antd-tree.css` itself, and this token has
 * no say over it.
 */
export const DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN = new InjectionToken<boolean>(
  'DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN',
);

/**
 * `LazyLoadService.load()` retries forever when it is given no retry count: its `retryWhen` notifier
 * concatenates the error stream with a `throwError`, and without a `take()` the error stream never
 * completes, so the `throwError` is never reached. A host that forgot the `styles` entry would then
 * re-request a 404 in a tight loop and never surface an error to subscribe to.
 *
 * `take()` doesn't buy what its count suggests, either. ABP's pipeline is
 * `retryWhen(error$ => concat(error$.pipe(delay(retryDelay), take(retryTimes)), throwError(...)))`,
 * and `take(n)` completes the notifier the instant its n-th error triggers a retry — not once that
 * retry settles — so `concat` subscribes `throwError` right away and tears the just-started retry
 * down before it can succeed. `n` therefore buys `n - 1` *real* retries: the n-th one is only ever
 * started and killed. `RETRY_TIMES = 2` means exactly one real retry — the second attempt gets to
 * actually succeed or fail — and, if it also fails, the failure is reported roughly one second (two
 * `RETRY_DELAY`s) after the first 404. The torn-down attempt's `<link>` is left behind in `<head>`;
 * harmless, since if it ever does finish loading, the CSS simply applies.
 */
const RETRY_TIMES = 2;
const RETRY_DELAY = 500;

/**
 * Loads the third-party global stylesheets this package family needs, by bundle name, once per app.
 *
 * Some of the components these packages render come with their own global CSS that is not part of
 * any Angular component's compiled styles — `<nz-select>`'s ng-zorro-antd stylesheet, CKEditor 5's
 * UI stylesheet. This service is how that CSS gets onto the page, on the same contract ABP itself
 * uses for `abp-tree` in `@abp/ng.components`: the host declares the third-party file in
 * `angular.json` under a fixed `bundleName`, and the component that needs it appends
 * `<link href="<bundleName>.css">` at init. The name is the contract; the file stays the host's own
 * copy of the third party's CSS.
 *
 * **Why by bundle name and not an `@import` from a component stylesheet.** Two separate reasons, one
 * per kind of dependency:
 *
 * 1. *A peer's CSS the bundler cannot reach at all.* ng-zorro-antd ships no component styles: every
 *    component is `ViewEncapsulation.None` and its CSS is a separate opt-in the application loads
 *    itself. `@import 'ng-zorro-antd/select/style/index.min.css'` does not resolve under the Angular
 *    CLI: ng-zorro-antd (21.0.2) declares every `./<component>/style/*` export as
 *    `{"less": "./<component>/style/*.less", "style": ".../index.min.css"}` — `less` first.
 *    `@angular/build` bundles stylesheets with `conditions: ['style', 'sass', 'less', …]`
 *    (`@angular/build/src/tools/esbuild/stylesheets/bundle-options.js`), Node's conditional-exports
 *    algorithm takes the first *declared* key that is in the condition set, so `less` wins and
 *    esbuild looks for `index.min.css.less`. Only `angular.json` `styles[].input` — a filesystem
 *    path, which bypasses `exports` — reaches those files.
 * 2. *A dependency's CSS the bundler does reach, and should not.* `ckeditor5` resolves
 *    `ckeditor5/ckeditor5.css` without trouble (`"./*": "./dist/*"`), and
 *    `@dignite/ng.flex-fields-ckeditor` used to `@import` it from `CKEditorControlComponent`'s
 *    stylesheet. ng-packagr inlines an `@import` at build time, so all 241 KB of that file became
 *    part of the package's JavaScript: the published `fesm2022` bundle was 471 KB carrying 522
 *    `.ck-editor` rules, and since a host registers the field type in its application config, that
 *    CSS landed in the host's *initial* bundle and was downloaded by every visitor whether or not a
 *    rich-text field was ever opened. It also splits the version in two: the host installs
 *    `ckeditor5` itself and the editor JavaScript comes from `await import('ckeditor5')` at runtime,
 *    so an `@import` pins the CSS to whatever version *this* package happened to be built against.
 *    Asked for by bundle name, both halves are the host's one installed copy.
 *
 * Shipping a copy of either stylesheet inside these packages was the third option and is rejected
 * for one reason covering both: it freezes a snapshot of someone else's CSS into an unrelated
 * release cycle, and the host then runs a third party's components against a different version's CSS
 * than the one it installed.
 *
 * Every bundle goes through this one service, so a host declares one entry per file however many
 * components ask for it — both `Select` components share {@link NZ_SELECT_STYLE}. See the "Styles"
 * section of the README of whichever package owns the constant for the host side.
 *
 * **Maintainers:** a package in this family that starts rendering a component with its own global
 * CSS defines a {@link StyleBundle} constant in *that* package, calls {@link load} from the rendering
 * component's `ngOnInit`, and updates that package's README "Styles" section and the changelog in the
 * same PR.
 */
@Injectable({ providedIn: 'root' })
export class FlexFieldsStyleLoader {
  private readonly lazyLoadService = inject(LazyLoadService);
  private readonly disabled =
    inject(DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN, { optional: true }) ?? false;

  /**
   * Bundles already asked for.
   *
   * `LazyLoadService` keeps its own `loaded` map, but only writes to it *after* a load succeeds, so
   * two components created in the same change-detection pass — a `Select` control and a `Select`
   * search on one page — would each append their own `<link>`. This is what makes it once per app.
   */
  private readonly requested = new Set<string>();

  /** Appends `<bundle.bundleName>.css` to `<head>`, unless it is already requested or disabled. */
  load(bundle: StyleBundle): void {
    if (this.disabled || this.requested.has(bundle.bundleName)) {
      return;
    }

    this.requested.add(bundle.bundleName);

    this.lazyLoadService
      .load(
        LOADING_STRATEGY.AppendAnonymousStyleToHead(`${bundle.bundleName}.css`),
        RETRY_TIMES,
        RETRY_DELAY,
      )
      // ABP's abp-tree leaves this stream's error unhandled, which surfaces as a bare
      // `CustomEvent {type: 'error'}` in the console with nothing naming the file or the fix. One
      // actionable message instead; the control keeps working, only unstyled.
      .subscribe({ error: () => this.reportMissingBundle(bundle) });
  }

  private reportMissingBundle(bundle: StyleBundle): void {
    console.error(
      `[@dignite/ng.flex-fields] Could not load "${bundle.bundleName}.css". This stylesheet is not ` +
        'compiled into the package: the host serves its own copy of it under a fixed bundle name. ' +
        'Add this entry to the "styles" array of your angular.json build target and rebuild:\n' +
        `  { "input": "${bundle.input}", "inject": false, "bundleName": "${bundle.bundleName}" }\n` +
        'Until then the control still works, only unstyled. See the "Styles" section of the ' +
        '@dignite/ng.flex-fields README, which also links the sibling packages\' own.',
    );
  }
}
