using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.Demo.Permissions;
using Dignite.Abp.FlexFields.Demo.Services.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Abp.FlexFields.Demo.Services;

/// <summary>
/// Manages <see cref="ProductField"/> definitions: which flex fields exist on <see cref="Product"/>, and
/// how each one is configured. Demonstrates the three operations the FlexFields kernel documents but never
/// performs itself, because it has no visibility into a downstream's own definition entity: creating a
/// field, renaming one, and deleting one.
/// </summary>
[Authorize]
public class ProductFieldAppService : DemoAppService
{
    private readonly IFlexFieldRepository<ProductField> _fieldRepository;
    private readonly IFlexFieldValueMigrator<Product> _valueMigrator;
    private readonly IFlexFieldIndexManager<Product> _indexManager;

    public ProductFieldAppService(
        IFlexFieldRepository<ProductField> fieldRepository,
        IFlexFieldValueMigrator<Product> valueMigrator,
        IFlexFieldIndexManager<Product> indexManager)
    {
        _fieldRepository = fieldRepository;
        _valueMigrator = valueMigrator;
        _indexManager = indexManager;
    }

    public virtual async Task<ProductFieldDto> GetAsync(Guid id)
    {
        var field = await _fieldRepository.GetAsync(id);
        return MapToDto(field);
    }

    public virtual async Task<PagedResultDto<ProductFieldDto>> GetListAsync(GetProductFieldListDto input)
    {
        var totalCount = await _fieldRepository.GetCountAsync();
        var fields = await _fieldRepository.GetPagedListAsync(input.SkipCount, input.MaxResultCount, sorting: nameof(ProductField.Name));

        return new PagedResultDto<ProductFieldDto>(totalCount, fields.ConvertAll(MapToDto));
    }

    [Authorize(DemoPermissions.ProductFields.Create)]
    public virtual async Task<ProductFieldDto> CreateAsync(CreateProductFieldDto input)
    {
        if (await _fieldRepository.NameExistsAsync(input.Name))
        {
            throw new BusinessException("Demo:FieldNameAlreadyExists").WithData("Name", input.Name);
        }

        var field = new ProductField(GuidGenerator.Create(), input.Name, input.DisplayName, input.FieldTypeName)
        {
            Description = input.Description,
            Required = input.Required,
            Searchable = input.Searchable,
            Configuration = input.Configuration.ToConfiguration(),
        };

        await _fieldRepository.InsertAsync(field);

        return MapToDto(field);
    }

    /// <summary>
    /// Updates a field definition. When <see cref="UpdateProductFieldDto.Name"/> differs from the field's
    /// current name, this is a rename, and the value-bag rewrite the kernel documents runs first - see
    /// <c>IFlexFieldValueMigrator{Product}.RenameFieldAsync</c>'s XML doc for why the ordering matters:
    /// renaming <see cref="ProductField.Name"/> before every product's bag has been rewritten would leave
    /// products holding a key nothing can reach.
    /// </summary>
    [Authorize(DemoPermissions.ProductFields.Update)]
    public virtual async Task<ProductFieldDto> UpdateAsync(Guid id, UpdateProductFieldDto input)
    {
        var field = await _fieldRepository.GetAsync(id);
        var wasSearchable = field.Searchable;

        if (!string.Equals(field.Name, input.Name, StringComparison.Ordinal))
        {
            if (await _fieldRepository.NameExistsAsync(input.Name, excludedId: field.Id))
            {
                throw new BusinessException("Demo:FieldNameAlreadyExists").WithData("Name", input.Name);
            }

            // 1) Rewrite every Product's value bag from the old key to the new one - before the
            //    definition's own Name changes, per IFlexFieldValueMigrator's documented ordering.
            await _valueMigrator.RenameFieldAsync(field.Name, input.Name);

            // 2) Only now does the definition itself take the new name.
            field.Name = input.Name;
        }

        field.DisplayName = input.DisplayName;
        field.Description = input.Description;
        field.Required = input.Required;
        field.Configuration = input.Configuration.ToConfiguration();
        field.Searchable = input.Searchable;

        await _fieldRepository.UpdateAsync(field);

        // Switching Searchable on (or off) changes which values belong in the index, and re-deriving from
        // the bag is a complete migration for that - see IFlexFieldIndexManager.RebuildAsync's XML doc.
        if (wasSearchable != field.Searchable)
        {
            await _indexManager.RebuildAsync();
        }

        return MapToDto(field);
    }

    [Authorize(DemoPermissions.ProductFields.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var field = await _fieldRepository.GetAsync(id);

        // Drop the now-orphaned values from every product's bag first - the kernel warns explicitly that
        // deleting the definition without this leaves values sitting in the bag forever, unreachable and
        // still serialized on every read.
        await _valueMigrator.RemoveFieldAsync(field.Name);

        await _fieldRepository.DeleteAsync(field);
    }

    private ProductFieldDto MapToDto(ProductField field)
    {
        var dto = ObjectMapper.Map<ProductField, ProductFieldDto>(field);
        dto.Configuration = field.Configuration.ToDictionary();
        return dto;
    }
}
