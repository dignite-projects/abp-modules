/**
 * The stylesheet-rewriting half of {@link DevThemeToggleComponent}, kept free of Angular so it can be
 * exercised directly against real stylesheets. Demo-only, never published.
 *
 * The demo has no dark mode to test against: it runs LeptonX **Lite**
 * (`@abp/ng.theme.lepton-x`, wrapping `@volo/ngx-lepton-x.lite`), which ships exactly one look - a
 * dark shell (`html, body { background-color: #161616 }`) around a light content area
 * (`.lpx-content-container { background-color: #f5f5f7 }`) with white cards - and contains no
 * theme-switching code whatsoever: nothing writes `.lpx-theme-dark` or `data-bs-theme`, and there is
 * a single `bootstrap-dim.css` bundle where full LeptonX ships separate light/dark/dim stylesheets.
 * With no theme to flip, a dark host has to be faked, which is all this does.
 */

/** One CSS declaration captured before it was rewritten, so the rewrite can be undone. */
export interface CapturedDeclaration {
  style: CSSStyleDeclaration;
  property: string;
  value: string;
  priority: string;
}

/**
 * Rewrites the light literals LeptonX Lite uses into dark ones across every stylesheet the document
 * can read, and returns what was overwritten so {@link restoreDarkRewrite} can put it all back.
 *
 * Only `href`-bearing sheets are walked - the external `<link>`ed bundles listed in `angular.json`,
 * which is where the theme's own colours live. Angular's component styles are injected as inline
 * `<style>` elements with no `href` and are skipped on purpose: a component hardcoding a colour is a
 * finding worth seeing here, not something to paper over. Cross-origin sheets throw on `cssRules`
 * and are skipped too.
 *
 * Reach is limited by construction, and knowing where it stops keeps its gaps from reading as bugs:
 *
 * - Only the literals in {@link ALWAYS}, {@link BACKGROUND_ONLY} and {@link TEXT_ONLY} are touched,
 *   as exact substrings. Anything using a different value passes through untouched - ng-zorro's
 *   components, for one, hardcode a completely disjoint palette (`#f5f5f5`, `#d9d9d9`, `#fff`,
 *   `rgba(0, 0, 0, 0.85)` and so on) and the per-component stylesheets this demo loads contain no
 *   `var()` at all, so they stay light under this switch. Giving them a dark mode means loading
 *   ng-zorro's own dark or variable build, not extending these maps.
 * - `--lpx-card-bg` is deliberately in scope for the background rewrite: LeptonX Lite pins it to a
 *   constant `#ffffff` at `:root` and never redeclares it, and it is the first link in
 *   `ckeditor-control.component.css`'s fallback chain, so without rewriting it the editor's
 *   background stays white however dark the rest of the page gets.
 * - Stylesheets added *after* this runs are not covered. Toggling off and on re-runs the walk.
 */
export function applyDarkRewrite(doc: Document): CapturedDeclaration[] {
  const captured: CapturedDeclaration[] = [];

  for (const sheet of Array.from(doc.styleSheets)) {
    if (!sheet.href) {
      continue;
    }

    let rules: CSSRuleList;
    try {
      rules = sheet.cssRules;
    } catch {
      continue;
    }

    rewriteRules(rules, captured);
  }

  return captured;
}

/**
 * Puts every captured declaration back, newest first. This is what makes the switch a *toggle*
 * rather than a one-way trip needing a page reload: the rewrite mutates live `CSSStyleDeclaration`s
 * in place and is not otherwise reversible.
 */
export function restoreDarkRewrite(captured: readonly CapturedDeclaration[]): void {
  for (let i = captured.length - 1; i >= 0; i--) {
    const { style, property, value, priority } = captured[i];
    style.setProperty(property, value, priority);
  }
}

function rewriteRules(rules: CSSRuleList, captured: CapturedDeclaration[]): void {
  for (const rule of Array.from(rules)) {
    // Nested rules first: @media and @supports blocks hold declarations of their own.
    const nested = (rule as Partial<CSSGroupingRule>).cssRules;
    if (nested) {
      rewriteRules(nested, captured);
    }

    const style = (rule as Partial<CSSStyleRule>).style;
    if (!style) {
      continue;
    }

    for (const property of Array.from(style)) {
      const original = style.getPropertyValue(property);
      const next = rewriteValue(property, original);
      if (next === original) {
        continue;
      }

      const priority = style.getPropertyPriority(property);
      captured.push({ style, property, value: original, priority });
      style.setProperty(property, next, priority);
    }
  }
}

/**
 * Light -> dark substitutions that hold whatever property uses them.
 *
 * Each is matched both as written and in `rgb()` notation, because reading a declaration back out of
 * the CSSOM does not always return the source text - browsers normalise colours in some positions,
 * so a stylesheet's `#f5f5f7` can come back as `rgb(245, 245, 247)`. Matching only the hex form
 * would silently miss those. The match is a plain substring match on six-digit hex, so a stylesheet
 * writing `#fff` is *not* matched by an entry for `#ffffff`.
 */
const ALWAYS = expand({
  '#f5f5f7': '#121212',
  '#e7e9ec': '#222222',
  '#686b6e': '#9ca5b4',
  '#9198a5': '#777d87',
  '#595c5f': '#ffffff',
});

/**
 * Substitutions valid only where the colour paints a surface, and only where it paints text. These
 * two are property-scoped because their source colours are role-ambiguous: `#ffffff` is a surface
 * when it backs something and a foreground when it paints text, and `#161616` is LeptonX Lite's
 * near-black page ground as a background but has to become *light* when it is text. Applying either
 * unconditionally would invert one of the two roles.
 */
const BACKGROUND_ONLY = expand({ '#ffffff': '#1b1b1b' });
const TEXT_ONLY = expand({ '#161616': '#eeeeee' });

/** Exported for testing: the whole substitution decision for one declaration. */
export function rewriteValue(property: string, value: string): string {
  let rewritten = replaceAll(value, ALWAYS);

  if (isBackgroundProperty(property)) {
    rewritten = replaceAll(rewritten, BACKGROUND_ONLY);
  }
  if (isTextProperty(property)) {
    rewritten = replaceAll(rewritten, TEXT_ONLY);
  }

  return rewritten;
}

function expand(map: Record<string, string>): readonly (readonly [string, string])[] {
  return Object.entries(map).flatMap(([from, to]) => [
    [from, to] as const,
    [toRgbNotation(from), to] as const,
  ]);
}

function toRgbNotation(hex: string): string {
  const [r, g, b] = [1, 3, 5].map(i => parseInt(hex.slice(i, i + 2), 16));
  return `rgb(${r}, ${g}, ${b})`;
}

function replaceAll(value: string, pairs: readonly (readonly [string, string])[]): string {
  return pairs.reduce((carry, [from, to]) => carry.split(from).join(to), value);
}

/** Custom properties are matched by name, because their values carry no hint of their role. */
function isBackgroundProperty(property: string): boolean {
  return property.startsWith('background') || /^--(bs|lpx)-.*-bg$/.test(property);
}

function isTextProperty(property: string): boolean {
  return property === 'color' || /^--bs-.*-color$/.test(property);
}
