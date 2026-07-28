using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FlexFields;

public class FieldTypeResolver : IFieldTypeResolver, ITransientDependency
{
    protected IEnumerable<IFieldType> FieldTypes { get; }

    public FieldTypeResolver(
        IEnumerable<IFieldType> fieldTypes)
    {
        FieldTypes = fieldTypes;
    }

    public virtual IFieldType Get(string fieldTypeName)
    {
        var fieldType = FieldTypes.SingleOrDefault(ft => ft.Name == fieldTypeName);

        if (fieldType == null)
            throw new AbpException(
                $"Could not find the field type with the name ({fieldTypeName}) ."
            );
        else
            return fieldType;
    }
}
