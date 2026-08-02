using System.Collections.Generic;

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

    /// <summary>
    /// Every registered field type, including any a downstream contributed of its own.
    /// <para>
    /// The kernel itself never needs this - it always resolves a field type by the name a stored field is
    /// bound to. It exists for the downstream: a field designer has to offer the admin a list to pick
    /// from, and whatever describes a field type to a client (its name, its label, whether it is
    /// indexable) has to be derived from this rather than hand-maintained a second time. The Angular
    /// library's <c>FieldTypeResolver.getAll()</c> is the same call on the other side.
    /// </para>
    /// </summary>
    IReadOnlyList<IFieldType> GetAll();
}
