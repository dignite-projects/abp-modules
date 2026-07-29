import { InjectionToken } from '@angular/core';

/** Derives a stable, URL-safe value from a human-readable label. */
export type FlexFieldSlugGenerator = (text: string) => string;

/**
 * Strips anything that is not a letter, digit, underscore or hyphen, and lowercases the rest.
 *
 * Deliberately does nothing clever with non-Latin scripts: a label written entirely in Chinese
 * reduces to an empty slug and the user is left to type a value themselves, which is honest. The old
 * library hard-wired pinyin transliteration (and a `pinyin-pro` dependency) into the library for this,
 * which is the wrong default for a general-purpose module — see {@link FLEX_FIELD_SLUG_GENERATOR}.
 */
export const defaultSlugGenerator: FlexFieldSlugGenerator = text =>
  (text ?? '')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9_-]+/g, '-')
    .replace(/^-+|-+$/g, '');

/**
 * How a node's value is suggested from its label in the tree designer.
 *
 * Optional, and defaulted to {@link defaultSlugGenerator}. Provide your own when your users write
 * labels in a script the default cannot transliterate:
 *
 * ```ts
 * import { pinyin } from 'pinyin-pro';
 *
 * providers: [
 *   { provide: FLEX_FIELD_SLUG_GENERATOR, useValue: (text: string) => pinyin(text, { toneType: 'none', type: 'array' }).join('-') },
 * ]
 * ```
 *
 * It only ever *suggests* — a value the user has already typed is never overwritten.
 */
export const FLEX_FIELD_SLUG_GENERATOR = new InjectionToken<FlexFieldSlugGenerator>(
  'FLEX_FIELD_SLUG_GENERATOR',
  { providedIn: 'root', factory: () => defaultSlugGenerator },
);
