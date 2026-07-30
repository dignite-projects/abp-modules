using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.FlexFields.Demo.Services.Dtos;

public class ProductDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Every flex field this product has, definition + Required/Searchable + current value - built from
    /// <c>IFlexFieldProvider&lt;Product&gt;.GetFlexFieldsAsync</c>, not straight off the entity. Hand this
    /// list directly to <c>&lt;ff-flex-field-view&gt;</c> on the client.
    /// </summary>
    public List<FlexFieldValueDto> FlexFieldValues { get; set; } = new();
}

public class CreateUpdateProductDto
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Submitted flex field values, keyed by <c>ProductField.Name</c> - the shape
    /// <c>&lt;ff-flex-field-control&gt;</c> writes into a host form's <c>flexFields</c> group. A key mapped
    /// to <c>null</c> clears that field rather than storing a null, matching
    /// <c>FlexFieldDictionaryExtensions.SetField</c>'s own semantics.
    /// </summary>
    public Dictionary<string, object?> FlexFields { get; set; } = new();
}

public class GetProductListDto : PagedResultRequestDto
{
    /// <summary>
    /// Narrows the result to products matching ALL of these - see
    /// <c>IFlexFieldQueryExecutor&lt;Product&gt;.ApplyFilterAsync</c>. Leave empty for an unfiltered list;
    /// the kernel rejects an empty list as a query, so <c>ProductAppService</c> only calls the executor
    /// when this is non-empty.
    /// </summary>
    public List<FlexFieldQueryConditionDto> FlexFieldConditions { get; set; } = new();
}

/// <summary>Wire shape of <c>FlexFieldQueryCondition</c>, one filter over one flex field.</summary>
public class FlexFieldQueryConditionDto
{
    public Guid FieldId { get; set; }

    public string FieldName { get; set; } = string.Empty;

    /// <summary><c>FlexFieldQueryOperator</c> ordinal - Equals=0, NotEquals=1, Contains=2, GreaterThan=3, GreaterThanOrEqual=4, LessThan=5, LessThanOrEqual=6, In=7.</summary>
    public FlexFieldQueryOperator Operator { get; set; }

    /// <summary>Comma-separated when <see cref="Operator"/> is <c>In</c>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary><c>FlexFieldValueType</c> ordinal - String=0, Number=1, DateTime=2, Boolean=3, Guid=4.</summary>
    public FlexFieldValueType ValueType { get; set; }
}
