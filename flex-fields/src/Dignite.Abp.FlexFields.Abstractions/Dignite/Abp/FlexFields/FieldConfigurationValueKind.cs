namespace Dignite.Abp.FlexFields;

/// <summary>
/// The JSON kind of a configuration value, as a <see cref="FieldConfigurationKey"/> declares it.
/// <para>
/// This is the shape of the value on the wire (what an API client sends and a JSON column stores), not a CLR
/// type: a configuration dictionary is a <c>Dictionary&lt;string, object&gt;</c> whose values come back from
/// storage as a <c>JsonElement</c>, a boxed number or a <c>List&lt;object&gt;</c>, depending on who read them.
/// </para>
/// </summary>
public enum FieldConfigurationValueKind
{
    /// <summary>A JSON string.</summary>
    String,

    /// <summary>A whole number that fits in 32 bits. A numeric string is read as well, as the Angular editors send one.</summary>
    Integer,

    /// <summary>A number, with or without a fraction. A numeric string is read as well.</summary>
    Number,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary>A date or a date and time, as an ISO 8601 string such as <c>2026-01-31</c> or <c>2026-01-31T10:00:00Z</c>.</summary>
    DateTime,

    /// <summary>A GUID, as a string.</summary>
    Guid,

    /// <summary>
    /// One member of an enum, as its <b>integer</b> value. A configuration is read by number, never by name:
    /// <c>"MultipleLine"</c> does not read back as <c>TextMode.MultipleLine</c>, <c>1</c> does.
    /// </summary>
    Enum,

    /// <summary>A JSON array.</summary>
    Array,

    /// <summary>A JSON object.</summary>
    Object
}
