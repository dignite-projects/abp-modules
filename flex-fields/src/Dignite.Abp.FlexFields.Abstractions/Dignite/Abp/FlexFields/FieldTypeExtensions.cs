namespace Dignite.Abp.FlexFields;

public static class FieldTypeExtensions
{
    /// <summary>
    /// Whether values of this field type can be recorded in the query index at all.
    /// <para>
    /// This is <see cref="IFieldType.IndexValueType"/> restated as the question a caller actually asks, and
    /// it is deliberately the module's answer to give rather than something each downstream re-derives. A
    /// field type with no index slot (<c>FileExplorer</c>, or a RichText/Matrix type a downstream adds) is
    /// skipped by <c>FlexFieldIndexManagerBase</c> <b>regardless of a field's <c>Searchable</c> flag</b>, so
    /// a downstream that lets an admin mark such a field searchable is offering a setting that silently
    /// does nothing. Two places need this: a field designer, to disable the setting, and the code that
    /// saves a field definition, to reject the combination outright.
    /// </para>
    /// </summary>
    public static bool IsIndexable(this IFieldType fieldType)
    {
        return fieldType.IndexValueType != null;
    }

    /// <summary>
    /// Whether a field of <paramref name="fieldTypeName"/> can meaningfully be marked searchable.
    /// Throws the same way <see cref="IFieldTypeResolver.Get"/> does when the name is not registered.
    /// </summary>
    public static bool IsIndexable(this IFieldTypeResolver resolver, string fieldTypeName)
    {
        return resolver.Get(fieldTypeName).IsIndexable();
    }
}
