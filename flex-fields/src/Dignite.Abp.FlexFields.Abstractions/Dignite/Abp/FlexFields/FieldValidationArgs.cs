namespace Dignite.Abp.FlexFields;

/// <summary>
/// Input to <see cref="IFieldType.Validate"/>. Deliberately thin for now - cross-field validation
/// will add a siblings collection later.
/// </summary>
public class FieldValidationArgs
{
    public FlexFieldValue Field { get; }

    public FieldValidationArgs(FlexFieldValue field)
    {
        Field = field;
    }
}
