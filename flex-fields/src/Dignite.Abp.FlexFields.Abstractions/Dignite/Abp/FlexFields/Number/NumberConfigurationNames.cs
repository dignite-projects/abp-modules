namespace Dignite.Abp.FlexFields.Number;

public static class NumberConfigurationNames
{
    public const string Max = "Number.Max";
    public const string Min = "Number.Min";

    /// <summary>
    /// Maximum number of decimal places after the decimal separator.
    /// </summary>
    public const string Decimals = "Number.Decimals";

    /// <summary>
    /// Specifies the interval between valid values.
    /// </summary>
    public const string Step = "Number.Step";

    /// <summary>
    /// Format specifier
    /// </summary>
    public const string FormatSpecifier = "FormatSpecifier";

    /// <summary>
    /// The placeholder of the input. The Angular <c>Number</c> components have always written and read it, so
    /// it is already stored in every field definition saved through them; the server named it only once it
    /// began checking configuration keys.
    /// </summary>
    public const string Placeholder = "Number.Placeholder";
}
