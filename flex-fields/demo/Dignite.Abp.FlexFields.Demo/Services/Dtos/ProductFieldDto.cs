using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.FlexFields.Demo.Services.Dtos;

public class ProductFieldDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string FieldTypeName { get; set; } = string.Empty;

    public Dictionary<string, object?> Configuration { get; set; } = new();

    public bool Required { get; set; }

    public bool Searchable { get; set; }
}

public class CreateProductFieldDto
{
    /// <summary>
    /// The bag key this field's values are stored under. Fixed at creation - renaming an existing field
    /// goes through <see cref="UpdateProductFieldDto"/>, which runs the migration steps this doesn't need.
    /// </summary>
    [Required]
    [StringLength(64)] // FlexFieldConsts.MaxNameLength's default - a mutable static, so not usable as an attribute argument
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(128)] // FlexFieldConsts.MaxDisplayNameLength's default
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(256)] // FlexFieldConsts.MaxDescriptionLength's default
    public string? Description { get; set; }

    [Required]
    [StringLength(64)] // FlexFieldConsts.MaxFieldTypeNameLength's default
    public string FieldTypeName { get; set; } = string.Empty;

    public Dictionary<string, object?> Configuration { get; set; } = new();

    public bool Required { get; set; }

    public bool Searchable { get; set; }
}

public class UpdateProductFieldDto
{
    /// <summary>
    /// The field's name as of this update. Different from the current stored name means this call is a
    /// rename, and <c>ProductFieldAppService.UpdateAsync</c> runs
    /// <c>IFlexFieldValueMigrator&lt;Product&gt;.RenameFieldAsync</c> before saving it.
    /// </summary>
    [Required]
    [StringLength(64)] // FlexFieldConsts.MaxNameLength's default - a mutable static, so not usable as an attribute argument
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(128)] // FlexFieldConsts.MaxDisplayNameLength's default
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(256)] // FlexFieldConsts.MaxDescriptionLength's default
    public string? Description { get; set; }

    public Dictionary<string, object?> Configuration { get; set; } = new();

    public bool Required { get; set; }

    /// <summary>
    /// Flipping this requires <c>IFlexFieldIndexManager&lt;Product&gt;.RebuildAsync()</c> afterwards -
    /// <c>ProductFieldAppService.UpdateAsync</c> does this automatically when it changes.
    /// </summary>
    public bool Searchable { get; set; }
}

public class GetProductFieldListDto : PagedResultRequestDto
{
}
