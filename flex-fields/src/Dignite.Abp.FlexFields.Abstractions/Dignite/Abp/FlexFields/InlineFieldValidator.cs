using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// Validates one repeated instance's values against its shared <see cref="InlineFieldDefinition"/>
/// schema, delegating each field to its own <see cref="IFieldType.Validate"/> - shared by
/// <c>MatrixFieldType</c> (once per block type, per block instance) and <c>TableFieldType</c> (the one
/// column schema, once per row), since both need the identical "resolve the inline field's own type,
/// build a synthetic <see cref="FlexFieldValue"/>, delegate, collect" recursion.
/// </summary>
internal static class InlineFieldValidator
{
    public static void AppendValidationErrors(
        List<ValidationResult> errors,
        IEnumerable<InlineFieldDefinition> fields,
        FlexFieldDictionary values,
        IReadOnlyList<IFieldType> fieldTypes,
        Func<InlineFieldDefinition, ValidationResult> unknownFieldTypeError,
        Func<InlineFieldDefinition, string, ValidationResult> invalidFieldError)
    {
        foreach (var field in fields)
        {
            // A field's own type can have been removed/renamed since this schema was authored -
            // IFieldTypeResolver.Get(name) would throw AbpException for that, too sharp a failure for a
            // validation pass to surface as anything other than one more error.
            var fieldType = fieldTypes.FirstOrDefault(ft => ft.Name == field.FieldTypeName);
            if (fieldType == null)
            {
                errors.Add(unknownFieldTypeError(field));
                continue;
            }

            values.TryGetValue(field.Name, out var value);

            var fieldData = new FlexFieldData(
                Guid.Empty,
                field.Name,
                field.DisplayName,
                field.FieldTypeName,
                field.Description,
                field.Configuration);

            var fieldValue = new FlexFieldValue(fieldData, field.Required, searchable: false, value);

            foreach (var error in fieldType.Validate(new FieldValidationArgs(fieldValue)))
            {
                errors.Add(invalidFieldError(field, error.ErrorMessage ?? string.Empty));
            }
        }
    }
}
