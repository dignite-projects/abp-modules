namespace Dignite.Abp.FlexFields;

public interface IFieldTypeResolver
{
    /// <summary>
    /// Get field type using name
    /// </summary>
    /// <param name="fieldTypeName">
    /// The <see cref="IFieldType.Name"/>
    /// </param>
    IFieldType Get(string fieldTypeName);
}
