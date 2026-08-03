/**
 * Configuration of a `Switch` field, shaped for `FormBuilder.group()`. Mirrors `BooleanConfiguration`
 * on the server.
 *
 * The stored key is `Switch.Default`, not `Boolean.Default` — `Switch` is the persisted registration
 * key and the configuration keys are built from it, independent of what the class itself is named.
 */
export class BooleanConfiguration {
  'Switch.Default': unknown = [false];
}
