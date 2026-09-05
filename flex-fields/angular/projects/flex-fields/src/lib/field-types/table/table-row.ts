/** One row - what a `Table` field's value is a list of. Mirrors the server's `TableRow`. Unlike
 * `MatrixBlockValue` there is no type-tag: every row shares the same `Table.Columns` schema. */
export interface TableRow {
  values: Record<string, unknown>;
}

/** A stored row in either casing - see `InlineFieldDefinition` for why both are read. */
type RawTableRow = Partial<{
  values: Record<string, unknown>;
  Values: Record<string, unknown>;
}>;

/** Reads a stored Table field's value - a list of rows - defensively. */
export function normalizeTableRows(source: unknown): TableRow[] {
  if (!Array.isArray(source)) {
    return [];
  }

  return source.map((item: unknown) => {
    const value = (item ?? {}) as RawTableRow;
    return { values: value.values ?? value.Values ?? {} };
  });
}
