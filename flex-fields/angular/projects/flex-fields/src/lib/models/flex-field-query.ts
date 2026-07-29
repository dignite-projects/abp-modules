import { FlexFieldValueType } from './flex-field-value-type';

/**
 * Comparison a {@link FlexFieldQueryCondition} applies against a field's indexed value. Mirrors
 * `FlexFieldQueryOperator` on the server — the ordinals are the wire format.
 */
export enum FlexFieldQueryOperator {
  Equals = 0,
  NotEquals = 1,
  /** Substring match against the field's indexed string slot. */
  Contains = 2,
  GreaterThan = 3,
  GreaterThanOrEqual = 4,
  LessThan = 5,
  LessThanOrEqual = 6,
  /** Matches when the indexed value is any of a comma-separated set carried in `value`. */
  In = 7,
}

/**
 * One filter over a flex field. Mirrors `FlexFieldQueryCondition` on the server.
 *
 * Both `fieldId` and `fieldName` are required: relational providers key on the id, document
 * providers can only address the value bag by name.
 */
export interface FlexFieldQueryCondition {
  fieldId: string;

  fieldName: string;

  operator: FlexFieldQueryOperator;

  /** Comma-separated when the operator is {@link FlexFieldQueryOperator.In}. */
  value: string;

  valueType: FlexFieldValueType;
}

/**
 * Operators each value type can be compared with — the client-side mirror of the server's
 * `FlexFieldQueryConditions` matrix, so a search UI cannot offer a filter the backend will reject.
 *
 * Ordering operators are limited to types with a natural order; `Contains` is substring matching and
 * so is text-only; `In` is set membership and so is pointless for a two-valued type.
 */
export const SUPPORTED_QUERY_OPERATORS: Readonly<
  Record<FlexFieldValueType, readonly FlexFieldQueryOperator[]>
> = {
  [FlexFieldValueType.String]: [
    FlexFieldQueryOperator.Equals,
    FlexFieldQueryOperator.NotEquals,
    FlexFieldQueryOperator.Contains,
    FlexFieldQueryOperator.In,
  ],
  [FlexFieldValueType.Number]: [
    FlexFieldQueryOperator.Equals,
    FlexFieldQueryOperator.NotEquals,
    FlexFieldQueryOperator.GreaterThan,
    FlexFieldQueryOperator.GreaterThanOrEqual,
    FlexFieldQueryOperator.LessThan,
    FlexFieldQueryOperator.LessThanOrEqual,
    FlexFieldQueryOperator.In,
  ],
  [FlexFieldValueType.DateTime]: [
    FlexFieldQueryOperator.Equals,
    FlexFieldQueryOperator.NotEquals,
    FlexFieldQueryOperator.GreaterThan,
    FlexFieldQueryOperator.GreaterThanOrEqual,
    FlexFieldQueryOperator.LessThan,
    FlexFieldQueryOperator.LessThanOrEqual,
    FlexFieldQueryOperator.In,
  ],
  [FlexFieldValueType.Boolean]: [
    FlexFieldQueryOperator.Equals,
    FlexFieldQueryOperator.NotEquals,
  ],
  [FlexFieldValueType.Guid]: [
    FlexFieldQueryOperator.Equals,
    FlexFieldQueryOperator.NotEquals,
    FlexFieldQueryOperator.In,
  ],
} as const;

export function isQueryOperatorSupported(
  valueType: FlexFieldValueType,
  operator: FlexFieldQueryOperator,
): boolean {
  return SUPPORTED_QUERY_OPERATORS[valueType]?.includes(operator) ?? false;
}
