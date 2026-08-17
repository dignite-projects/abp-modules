/**
 * Normalizes a stored flex-field value into a list of strings — the client-side mirror of
 * `FieldTypeBase.ReadStringList` on the server.
 *
 * Multi-valued field types (`Select`, `Tree`) can find their value stored as an array, as a bare
 * scalar (a single-select that was later switched to multiple), or as `null`. Every one of those has
 * to read as a list, and both halves of the stack have to agree on how — otherwise a value the
 * server considers set reads as empty in the browser, or the reverse.
 *
 * Note that an empty string yields `['']`, not `[]`, matching the server. Callers that need
 * "is anything actually selected" must check the contents, not just the length.
 */
export function readStringList(value: unknown): string[] {
  if (value === null || value === undefined) {
    return [];
  }

  // Before the array check: a string is a sequence of chars, and 'abc' is one value, not three.
  if (typeof value === 'string') {
    return [value];
  }

  if (Array.isArray(value)) {
    return value.filter(item => item !== null && item !== undefined).map(item => `${item}`);
  }

  return [`${value}`];
}
