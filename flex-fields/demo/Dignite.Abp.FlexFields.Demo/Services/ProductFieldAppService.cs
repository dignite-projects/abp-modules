using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IFieldTypeResolver _fieldTypeResolver;

    public ProductFieldAppService(
        IFlexFieldRepository<ProductField> fieldRepository,
        IFlexFieldValueMigrator<Product> valueMigrator,
        IFlexFieldIndexManager<Product> indexManager,
        IFieldTypeResolver fieldTypeResolver)
    {
        _fieldRepository = fieldRepository;
        _valueMigrator = valueMigrator;
        _indexManager = indexManager;
        _fieldTypeResolver = fieldTypeResolver;
    }

    public virtual async Task<ProductFieldDto> GetAsync(Guid id)
    {
        var field = await _fieldRepository.GetAsync(id);
        return MapToDto(field);
    }

    /// <summary>
    /// The registered field types and what the client cannot derive about them - see
    /// <see cref="FieldTypeDto"/>. <c>GET /api/app/product-field/field-types</c>.
    /// <para>
    /// The kernel has no application layer, so serving this is a downstream's job; this is the worked
    /// example. It exists so the field designer's "searchable" setting is driven by the server's own
    /// <see cref="IFieldType.IndexValueType"/> instead of a copy of it maintained by hand on the client -
    /// a copy nothing in either build could catch drifting.
    /// </para>
    /// </summary>
    public virtual Task<List<FieldTypeDto>> GetFieldTypesAsync()
    {
        var fieldTypes = _fieldTypeResolver.GetAll()
            .Select(fieldType => new FieldTypeDto
            {
                Name = fieldType.Name,
                Indexable = fieldType.IsIndexable(),
            })
            .ToList();

        return Task.FromResult(fieldTypes);
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

        CheckSearchable(input.FieldTypeName, input.Searchable);

        var configuration = input.Configuration.ToConfiguration();
        CheckNestingDepth(input.FieldTypeName, configuration);

        var field = new ProductField(GuidGenerator.Create(), input.Name, input.DisplayName, input.FieldTypeName)
        {
            Description = input.Description,
            Required = input.Required,
            Searchable = input.Searchable,
            Configuration = configuration,
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

        // The field type is fixed at creation, so this checks the stored one against the incoming flag.
        CheckSearchable(field.FieldTypeName, input.Searchable);

        var configuration = input.Configuration.ToConfiguration();
        CheckNestingDepth(field.FieldTypeName, configuration);

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
        field.Configuration = configuration;
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

    /// <summary>
    /// Rejects a field marked searchable under a field type with no query-index slot.
    /// <para>
    /// The Angular field designer already disables the setting for such a type, but that is a courtesy to
    /// the admin, not the rule: anything reaching this service another way - a direct API call, a future
    /// admin UI, a data seeder - would otherwise store a flag that <c>FlexFieldIndexManagerBase</c>
    /// silently ignores, and the only symptom would be a search that never matches. Failing the save is
    /// the difference between a mistake that is reported and one that is invisible.
    /// </para>
    /// </summary>
    private void CheckSearchable(string fieldTypeName, bool searchable)
    {
        if (searchable && !_fieldTypeResolver.IsIndexable(fieldTypeName))
        {
            throw new BusinessException("Demo:FieldTypeNotSearchable")
                .WithData("FieldType", _fieldTypeResolver.Get(fieldTypeName).DisplayName);
        }
    }

    /// <summary>
    /// Rejects a configuration that nests composite field types (Matrix, Table) deeper than
    /// <see cref="CompositeFieldNesting.MaxDepth"/> - see <see cref="CompositeFieldNesting"/> for why the
    /// tree has to be capped at all, and why the cap belongs on the write path rather than in whatever
    /// walks the configuration afterwards.
    /// <para>
    /// The Angular field designer stops offering composite types once the limit is reached, but that is
    /// the same kind of courtesy <see cref="CheckSearchable"/>'s counterpart is: the rule has to hold for
    /// anything that reaches this service another way.
    /// </para>
    /// </summary>
    private void CheckNestingDepth(string fieldTypeName, FieldConfigurationDictionary configuration)
    {
        if (CompositeFieldNesting.ExceedsMaxDepth(fieldTypeName, configuration, _fieldTypeResolver.GetAll()))
        {
            throw new BusinessException("Demo:FieldNestingTooDeep")
                .WithData("MaxDepth", CompositeFieldNesting.MaxDepth);
        }
    }

    private ProductFieldDto MapToDto(ProductField field)
    {
        var dto = ObjectMapper.Map<ProductField, ProductFieldDto>(field);
        dto.Configuration = field.Configuration.ToDictionary();
        return dto;
    }
}
