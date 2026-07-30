using System;
using System.Collections.Generic;

namespace Dignite.Abp.FlexFields.Demo.Services.Dtos;

/// <summary>
/// Wire shape of a flex field's definition, matching the <c>FlexFieldData</c> interface the
/// <c>@dignite/ng.flex-fields</c> Angular library's <c>&lt;ff-flex-field-*&gt;</c> components bind to
/// directly - see <c>flex-fields/angular/projects/flex-fields/src/lib/models/flex-field-data.ts</c>.
/// </summary>
public class FlexFieldDataDto
{
    public Guid Id { get; set; } = default!;

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Registration key of the field type this field is bound to, e.g. <c>"TextEdit"</c>.</summary>
    public string FieldTypeName { get; set; } = string.Empty;

    public Dictionary<string, object?> Configuration { get; set; } = new();
}

/// <summary>
/// Wire shape of <c>FlexFieldValue</c> - one field's definition, its per-usage flags, and its current
/// value - matching the Angular library's <c>FlexFieldValue</c> model exactly, so a <see cref="ProductDto"/>
/// can be handed straight to <c>&lt;ff-flex-field-control&gt;</c> / <c>&lt;ff-flex-field-view&gt;</c> /
/// <c>&lt;ff-flex-field-search&gt;</c> without reshaping on the client.
/// </summary>
public class FlexFieldValueDto
{
    public FlexFieldDataDto Field { get; set; } = default!;

    public bool Required { get; set; }

    public bool Searchable { get; set; }

    public object? Value { get; set; }
}
