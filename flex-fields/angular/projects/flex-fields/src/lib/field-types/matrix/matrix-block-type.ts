import type { InlineFieldDefinition } from '../inline-field-definition';
import { normalizeInlineFieldDefinitions } from '../inline-field-definition';

/** One block type a `Matrix` field's configuration declares. Mirrors `MatrixBlockType`. */
export interface MatrixBlockType {
  name: string;
  displayName: string;
  fields: InlineFieldDefinition[];
}

/** One block instance - what a `Matrix` field's value is a list of. Mirrors `MatrixBlockValue`. */
export interface MatrixBlockValue {
  blockTypeName: string;
  values: Record<string, unknown>;
}

/** A stored block type in either casing - see {@link InlineFieldDefinition} for why both are read. */
type RawMatrixBlockType = Partial<{
  name: string;
  Name: string;
  displayName: string;
  DisplayName: string;
  fields: unknown;
  Fields: unknown;
}>;

/** A stored block instance in either casing - see {@link InlineFieldDefinition} for why both are read. */
type RawMatrixBlockValue = Partial<{
  blockTypeName: string;
  BlockTypeName: string;
  values: Record<string, unknown>;
  Values: Record<string, unknown>;
}>;

/** Reads a stored `Matrix.BlockTypes` configuration value, defensively. */
export function normalizeMatrixBlockTypes(source: unknown): MatrixBlockType[] {
  if (!Array.isArray(source)) {
    return [];
  }

  return source.map((item: unknown) => {
    const value = (item ?? {}) as RawMatrixBlockType;
    return {
      name: value.name ?? value.Name ?? '',
      displayName: value.displayName ?? value.DisplayName ?? '',
      fields: normalizeInlineFieldDefinitions(value.fields ?? value.Fields),
    };
  });
}

/** Reads a stored Matrix field's value - a list of block instances - defensively. */
export function normalizeMatrixBlockValues(source: unknown): MatrixBlockValue[] {
  if (!Array.isArray(source)) {
    return [];
  }

  return source.map((item: unknown) => {
    const value = (item ?? {}) as RawMatrixBlockValue;
    return {
      blockTypeName: value.blockTypeName ?? value.BlockTypeName ?? '',
      values: value.values ?? value.Values ?? {},
    };
  });
}
