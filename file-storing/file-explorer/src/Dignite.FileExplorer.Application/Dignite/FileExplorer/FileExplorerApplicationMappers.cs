using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Files;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Dignite.FileExplorer;

// Object mapping runs on Mapperly (compile-time source generators) instead of AutoMapper, which
// keeps the published packages free of AutoMapper's advisory (GHSA-rvv3-g6hj-g44x) and matches how
// ABP's own modules map. RequiredMappingStrategy.Target makes an unmapped destination property a
// build error, so every target is either mapped by name or explicitly ignored below.

/// <summary>
/// <see cref="FileDescriptor"/> -> <see cref="FileDescriptorDto"/>. <c>Url</c> is not a stored field;
/// it is resolved per-request from the blob provider by <c>FileDescriptorAppService</c>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FileDescriptorToDtoMapper : MapperBase<FileDescriptor, FileDescriptorDto>
{
    [MapperIgnoreTarget(nameof(FileDescriptorDto.Url))]
    public override partial FileDescriptorDto Map(FileDescriptor source);

    [MapperIgnoreTarget(nameof(FileDescriptorDto.Url))]
    public override partial void Map(FileDescriptor source, FileDescriptorDto destination);
}

/// <summary>
/// <see cref="DirectoryDescriptor"/> -> <see cref="DirectoryDescriptorDto"/>. All properties map 1:1;
/// <see cref="MapExtraPropertiesAttribute"/> carries the ABP <c>ExtraProperties</c> bag across.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
[MapExtraProperties]
public partial class DirectoryDescriptorToDtoMapper : MapperBase<DirectoryDescriptor, DirectoryDescriptorDto>
{
    public override partial DirectoryDescriptorDto Map(DirectoryDescriptor source);
    public override partial void Map(DirectoryDescriptor source, DirectoryDescriptorDto destination);
}

/// <summary>
/// <see cref="DirectoryDescriptor"/> -> <see cref="DirectoryDescriptorInfoDto"/>. <c>Children</c> is the
/// directory tree, assembled by <c>DirectoryDescriptorAppService</c> rather than mapped from the entity.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
[MapExtraProperties]
public partial class DirectoryDescriptorToInfoDtoMapper : MapperBase<DirectoryDescriptor, DirectoryDescriptorInfoDto>
{
    [MapperIgnoreTarget(nameof(DirectoryDescriptorInfoDto.Children))]
    public override partial DirectoryDescriptorInfoDto Map(DirectoryDescriptor source);

    [MapperIgnoreTarget(nameof(DirectoryDescriptorInfoDto.Children))]
    public override partial void Map(DirectoryDescriptor source, DirectoryDescriptorInfoDto destination);
}
