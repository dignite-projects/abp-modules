using System;
using Dignite.Abp.FlexFields.Demo.Entities;
using Dignite.Abp.FlexFields.Demo.Services.Dtos;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Dignite.Abp.FlexFields.Demo.ObjectMapping;

// Configuration (ProductField <-> its DTOs) and FlexFieldValues (Product -> ProductDto) are deliberately
// excluded from every mapper below and filled in by hand in the app services instead:
//  - Configuration's entity-side type is FieldConfigurationDictionary, a FlexFields kernel type Mapperly
//    has no reason to know how to construct; FlexFieldDtoMapping's ToConfiguration()/ToDictionary() are
//    clearer than teaching the generator a custom conversion for one field.
//  - FlexFieldValues isn't on Product at all - it comes from IFlexFieldProvider<Product>.GetFlexFieldsAsync,
//    a call with a dependency a mapper shouldn't have.

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ProductFieldToProductFieldDtoMapper : MapperBase<ProductField, ProductFieldDto>
{
    [MapperIgnoreTarget(nameof(ProductFieldDto.Configuration))]
    public override partial ProductFieldDto Map(ProductField source);

    [MapperIgnoreTarget(nameof(ProductFieldDto.Configuration))]
    public override partial void Map(ProductField source, ProductFieldDto destination);
}

/// <summary>
/// <see cref="ProductField"/> is an <c>AggregateRoot&lt;Guid&gt;</c> with no public parameterless
/// constructor, so Mapperly cannot generate <see cref="Map(CreateProductFieldDto)"/> the way it generates
/// entity -&gt; DTO mappers. <see cref="ProductFieldAppService"/> never calls this overload - it constructs
/// the entity itself and calls <see cref="Map(CreateProductFieldDto, ProductField)"/> directly, the same
/// "public glue + generated partial" split Mapperly recommends for constructor-shaped targets. This
/// overload exists only because <c>MapperBase&lt;,&gt;</c> declares it abstract.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class CreateProductFieldDtoToProductFieldMapper : MapperBase<CreateProductFieldDto, ProductField>
{
    public override ProductField Map(CreateProductFieldDto source)
    {
        var destination = new ProductField(Guid.NewGuid(), source.Name, source.DisplayName, source.FieldTypeName);
        Map(source, destination);
        return destination;
    }

    [MapperIgnoreTarget(nameof(ProductField.Configuration))]
    [MapperIgnoreTarget(nameof(ProductField.Id))]
    [MapperIgnoreTarget(nameof(ProductField.Name))] // already set by the constructor above
    [MapperIgnoreTarget(nameof(ProductField.FieldTypeName))] // already set by the constructor above
    [MapperIgnoreTarget(nameof(ProductField.ConcurrencyStamp))] // ABP/EF Core manages this, not a DTO field
    public override partial void Map(CreateProductFieldDto source, ProductField destination);
}

/// <summary>See the remarks on <see cref="CreateProductFieldDtoToProductFieldMapper"/> - same reason, same shape.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class UpdateProductFieldDtoToProductFieldMapper : MapperBase<UpdateProductFieldDto, ProductField>
{
    public override ProductField Map(UpdateProductFieldDto source)
    {
        // Update semantics have no meaning without an id - ProductFieldAppService never calls this
        // overload, only Map(source, destination) against an entity it already loaded.
        throw new NotSupportedException(
            $"{nameof(UpdateProductFieldDtoToProductFieldMapper)} only supports updating an existing " +
            $"{nameof(ProductField)} via {nameof(Map)}({nameof(UpdateProductFieldDto)}, {nameof(ProductField)}).");
    }

    [MapperIgnoreTarget(nameof(ProductField.Configuration))]
    [MapperIgnoreTarget(nameof(ProductField.Id))]
    [MapperIgnoreTarget(nameof(ProductField.Name))] // renaming is ProductFieldAppService.UpdateAsync's job, not a plain field copy
    [MapperIgnoreTarget(nameof(ProductField.FieldTypeName))] // a field's type never changes after creation in this demo
    [MapperIgnoreTarget(nameof(ProductField.ConcurrencyStamp))] // ABP/EF Core manages this, not a DTO field
    public override partial void Map(UpdateProductFieldDto source, ProductField destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ProductToProductDtoMapper : MapperBase<Product, ProductDto>
{
    [MapperIgnoreTarget(nameof(ProductDto.FlexFieldValues))]
    public override partial ProductDto Map(Product source);

    [MapperIgnoreTarget(nameof(ProductDto.FlexFieldValues))]
    public override partial void Map(Product source, ProductDto destination);
}
