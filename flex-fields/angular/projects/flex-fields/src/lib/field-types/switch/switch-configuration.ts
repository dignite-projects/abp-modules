/**
 * Configuration of a `Switch` field, shaped for `FormBuilder.group()`. Mirrors `SwitchConfiguration`
 * on the server.
 *
 * Named after the folder rather than the component, exactly as on the server: the field type class is
 * `BooleanFieldType` but its configuration is `SwitchConfiguration`, because `Switch` is the stored
 * registration key and the configuration keys are built from it.
 */
export class SwitchConfiguration {
  'Switch.Default': unknown = [false];
}
