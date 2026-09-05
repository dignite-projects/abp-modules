/**
 * One field defined inline as part of a composite field type's own configuration - a `Matrix` block
 * type's sub-field, or a `Table` column. Mirrors `InlineFieldDefinition`
 * (`src/Dignite.Abp.FlexFields.Abstractions/Dignite/Abp/FlexFields/InlineFieldDefinition.cs`), shared
 * between the two rather than each declaring its own copy.
 *
 * camelCase is canonical - it is what the Angular designer writes into a stored configuration, and what
 * `INormalizesValue` produces for values - so it is the only casing these readers ever *write*. They
 * *read* either one, though: a field definition can also be authored server-side from the typed C#
 * configuration classes (`new TableConfiguration { Columns = ... }` - the demo's
 * `ProductDemoDataSeedContributor` is the worked example), and EF Core's JSON value converter serializes
 * those with System.Text.Json's *default* options, so what lands in the database and comes back over the
 * API is PascalCase: `{"Name":"title","FieldTypeName":"Text","Configuration":{...}}`. The server's own
 * readers already accept either casing (`JsonSerializerDefaults.Web`), so these are the client-side half
 * of the same lenience - same dual-casing round trip as `Select.Options`/`SelectListItem`, in the
 * opposite direction.
 */
export interface InlineFieldDefinition {
  name: string;
  displayName: string;
  description?: string;
  fieldTypeName: string;
  required: boolean;
  configuration: Record<string, unknown>;
}

/** A stored inline field definition in either casing - see {@link InlineFieldDefinition} for why both. */
type RawInlineFieldDefinition = Partial<{
  name: string;
  Name: string;
  displayName: string;
  DisplayName: string;
  description: string;
  Description: string;
  fieldTypeName: string;
  FieldTypeName: string;
  required: boolean;
  Required: boolean;
  configuration: Record<string, unknown>;
  Configuration: Record<string, unknown>;
}>;

function normalizeInlineFieldDefinition(item: unknown): InlineFieldDefinition {
  const source = (item ?? {}) as RawInlineFieldDefinition;
  return {
    name: source.name ?? source.Name ?? '',
    displayName: source.displayName ?? source.DisplayName ?? '',
    description: source.description ?? source.Description,
    fieldTypeName: source.fieldTypeName ?? source.FieldTypeName ?? '',
    required: source.required ?? source.Required ?? false,
    configuration: source.configuration ?? source.Configuration ?? {},
  };
}

/** Reads a stored `Matrix.BlockTypes[].fields` or `Table.Columns` configuration value, defensively. */
export function normalizeInlineFieldDefinitions(source: unknown): InlineFieldDefinition[] {
  return Array.isArray(source) ? source.map(normalizeInlineFieldDefinition) : [];
}
