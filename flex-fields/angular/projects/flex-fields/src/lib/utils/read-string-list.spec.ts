import { readStringList } from './read-string-list';

describe('readStringList', () => {
  it('reads null and undefined as empty', () => {
    expect(readStringList(null)).toEqual([]);
    expect(readStringList(undefined)).toEqual([]);
  });

  it('reads a string as one value, not a sequence of characters', () => {
    expect(readStringList('abc')).toEqual(['abc']);
  });

  // Matches the server's ReadStringList. Callers that mean "is anything selected" have to look at
  // the contents, because '' is a value as far as both sides are concerned.
  it('reads an empty string as one empty value, not as empty', () => {
    expect(readStringList('')).toEqual(['']);
  });

  it('reads an array through, stringifying members', () => {
    expect(readStringList(['a', 'b'])).toEqual(['a', 'b']);
    expect(readStringList([1, 2])).toEqual(['1', '2']);
  });

  it('drops null and undefined members of an array', () => {
    expect(readStringList(['a', null, undefined, 'b'])).toEqual(['a', 'b']);
  });

  it('reads a bare scalar as a single value', () => {
    expect(readStringList(7)).toEqual(['7']);
  });
});
