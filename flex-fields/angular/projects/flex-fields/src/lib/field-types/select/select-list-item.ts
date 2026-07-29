/**
 * One choice in a `Select` field. Mirrors `SelectListItem` on the server.
 *
 * Pascal-cased because these are the keys written into the stored `Select.Options` array by the
 * designer. ABP's JSON serializer camel-cases on the way out, though, so anything read back from the
 * server arrives as `{ text, value, selected }` — run it through {@link normalizeSelectListItem}
 * before use rather than reading either casing at every call site.
 */
export interface SelectListItem {
  Text: string;
  Value: string;
  Selected: boolean;
}

/** A stored option in either casing, as it may arrive from the designer or from the server. */
type RawSelectListItem = Partial<
  SelectListItem & { text: string; value: string; selected: boolean }
>;

/** Reads an option written in either casing into the Pascal-cased shape the components use. */
export function normalizeSelectListItem(item: RawSelectListItem): SelectListItem {
  return {
    Text: item.Text ?? item.text ?? '',
    Value: item.Value ?? item.value ?? '',
    Selected: item.Selected ?? item.selected ?? false,
  };
}

/** Reads a stored `Select.Options` value, whatever casing and shape it arrived in. */
export function normalizeSelectListItems(options: unknown): SelectListItem[] {
  return Array.isArray(options) ? options.map(normalizeSelectListItem) : [];
}
