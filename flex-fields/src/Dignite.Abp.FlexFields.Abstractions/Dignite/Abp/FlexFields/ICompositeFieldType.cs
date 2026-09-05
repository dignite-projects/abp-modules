using System.Collections.Generic;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// A field type whose <i>configuration</i> declares further fields inline - <c>Matrix</c> (a block
/// type's sub-fields) and <c>Table</c> (the shared column schema). To <see cref="IFieldType"/> at large
/// a configuration is an opaque bag, so "does a field of this type contain other fields" has nowhere
/// else to be asked.
///
/// <para>
/// <b>Why an interface rather than a bool.</b> Every caller that cares about compositeness also needs to
/// walk the nested fields - measuring nesting depth
/// (<see cref="CompositeFieldNesting"/>), validating recursively, describing a schema. A bare
/// <c>IsComposite</c> flag would leave each of those to switch on the concrete type to get at the
/// inline fields, which is exactly the coupling this replaces.
/// </para>
///
/// <para>
/// <b>Why it lives in the kernel.</b> A host has to be able to ask "is this field type composite, and
/// what does it declare inside" without knowing <see cref="Matrix.MatrixFieldType"/> or
/// <see cref="Table.TableFieldType"/> by name - that is exactly what capping nesting depth on a
/// definition save (<see cref="CompositeFieldNesting.ExceedsMaxDepth"/>) needs, and it is the host,
/// not the field type, that owns the definition-save path. A third composite type - one a downstream
/// adds - is covered by the same rule the moment it implements this, with nothing to register.
/// </para>
/// </summary>
public interface ICompositeFieldType : IFieldType
{
    /// <summary>
    /// The fields this type's configuration declares inline, flattened - <c>Matrix</c> returns every
    /// block type's fields together, since nesting depth and type-picker eligibility do not care which
    /// block a sub-field belongs to.
    /// </summary>
    IEnumerable<InlineFieldDefinition> GetInlineFields(FieldConfigurationDictionary configuration);
}
