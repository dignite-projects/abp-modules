/**
 * Which typed slot a field's value belongs in. Mirrors the `FlexFieldValueType` enum on the server —
 * the ordinals are the wire format, so the order here is fixed.
 */
export enum FlexFieldValueType {
  String = 0,
  Number = 1,
  DateTime = 2,
  Boolean = 3,
  Guid = 4,
}
