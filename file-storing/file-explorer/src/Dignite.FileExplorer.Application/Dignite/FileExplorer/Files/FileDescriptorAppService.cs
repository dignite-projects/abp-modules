using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using Dignite.Abp.FileStoring.Imaging;
using Dignite.FileExplorer.Directories;
using Dignite.FileExplorer.Permissions;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using SixLabors.ImageSharp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.Imaging;
using Volo.Abp.Threading;
using Volo.Abp;

namespace Dignite.FileExplorer.Files;

public class FileDescriptorAppService : ApplicationService, IFileDescriptorAppService
{
    private const int MaxResizeDimension = 4096;
    private const long MaxResizePixelCount = 16_000_000;
    private const int MaxDecompressionRatio = 100;
    private const int DecodeTimeoutSeconds = 10;
    private const int MaxCachedImageBytes = 8 * 1024 * 1024;
    private static readonly IMemoryCache FallbackImageResizeCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 64 * 1024 * 1024
    });

    private readonly IFileDescriptorRepository _fileRepository;
    private readonly IDirectoryDescriptorRepository _directoryRepository;
    private readonly FileDescriptorManager _fileManager;
    private readonly IBlobContainerFactory _blobContainerFactory;
    private readonly IBlobContainerConfigurationProvider _blobContainerConfigurationProvider;
    private readonly IImageResizer _imageResizer;
    private readonly IMemoryCache _imageResizeCache;
    private readonly ContainerNameValidator _containerNameValidator;

    private CancellationToken RequestCancellationToken =>
        LazyServiceProvider.LazyGetService<ICancellationTokenProvider>()?.Token ?? CancellationToken.None;

    public FileDescriptorAppService(
        IFileDescriptorRepository fileRepository,
        IDirectoryDescriptorRepository directoryRepository,
        FileDescriptorManager fileManager,
        IBlobContainerFactory blobContainerFactory,
        IBlobContainerConfigurationProvider blobContainerConfigurationProvider,
        IImageResizer imageResizer,
        ContainerNameValidator containerNameValidator = null)
    {
        _fileRepository = fileRepository;
        _directoryRepository = directoryRepository;
        _fileManager = fileManager;
        _blobContainerFactory = blobContainerFactory;
        _blobContainerConfigurationProvider = blobContainerConfigurationProvider;
        _imageResizer = imageResizer;
        // Keep resized images in a cache owned by this module. The host's shared
        // IMemoryCache may have unrelated entries and must not determine this
        // endpoint's eviction policy.
        _imageResizeCache = FallbackImageResizeCache;
        _containerNameValidator = containerNameValidator ?? new ContainerNameValidator(blobContainerConfigurationProvider);
    }

    [Authorize]
    public async Task<FileDescriptorDto> CreateAsync(CreateFileInput input)
    {
        var cancellationToken = RequestCancellationToken;
        // Build a temporary file for authorization verification
        var tempFileDescriptor = new FileDescriptor(GuidGenerator.Create(), input.ContainerName, string.Empty, string.Empty, string.Empty, input.CellName, input.DirectoryId, input.EntityId, CurrentTenant.Id);
        await AuthorizationService.CheckAsync(tempFileDescriptor, CommonOperations.Create);

        // formal start of file creation
        var fileDescriptor = await _fileManager.CreateAsync(
            input.ContainerName,
            input.File,
            input.CellName,
            input.DirectoryId,
            input.EntityId,
            cancellationToken);
        return ObjectMapper.Map<FileDescriptor, FileDescriptorDto>(fileDescriptor);
    }

    [Authorize]
    public async Task<FileDescriptorDto> UpdateAsync(Guid id, UpdateFileInput input)
    {
        var cancellationToken = RequestCancellationToken;
        var entity = await _fileRepository.GetAsync(id, cancellationToken: cancellationToken);
        _containerNameValidator.Validate(entity.ContainerName);

        if (input.DirectoryIdSpecified)
        {
            entity.MoveToDirectory(input.DirectoryId);
        }

        if (input.Name != null)
        {
            entity.Rename(input.Name);
        }

        if (input.CellNameSpecified)
        {
            entity.SetCell(input.CellName);
        }

        await AuthorizationService.CheckAsync(entity, CommonOperations.Update);

        if (entity.DirectoryId.HasValue)
        {
            var directory = await _directoryRepository.FindAsync(
                entity.DirectoryId.Value,
                false,
                cancellationToken);
            if (directory == null ||
                !directory.ContainerName.Equals(entity.ContainerName, StringComparison.CurrentCultureIgnoreCase) ||
                directory.CreatorId != entity.CreatorId ||
                directory.TenantId != entity.TenantId)
            {
                throw new BusinessException(FileExplorerErrorCodes.Directories.DirectoryNotExist);
            }
        }

        await _fileManager.ValidateAsync(entity);
        await _fileRepository.UpdateAsync(entity, cancellationToken: cancellationToken);
        return ObjectMapper.Map<FileDescriptor, FileDescriptorDto>(entity);
    }

    [Authorize]
    public async Task DeleteAsync(Guid id)
    {
        var cancellationToken = RequestCancellationToken;
        var result = await _fileRepository.FindAsync(id, false, cancellationToken);
        if (result != null)
        {
            _containerNameValidator.Validate(result.ContainerName);
            await AuthorizationService.CheckAsync(result, CommonOperations.Delete);
            await _fileManager.DeleteAsync(result, cancellationToken);
        }
    }

    [Authorize]
    public async Task DeleteByEntityIdAsync([NotNull] string containerName, string entityId)
    {
        var cancellationToken = RequestCancellationToken;
        _containerNameValidator.Validate(containerName);
        var allowDelete = await AuthorizationService.IsGrantedAsync(FileExplorerPermissions.Files.Management);
        var creatorId = allowDelete?null:CurrentUser.Id;
        var result = await _fileRepository.GetListAsync(
            containerName,
            creatorId,
            null,
            null,
            entityId,
            cancellationToken: cancellationToken);
        foreach (var file in result)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AuthorizationService.CheckAsync(file, CommonOperations.Delete);
            await _fileManager.DeleteAsync(file, cancellationToken);
        }
    }

    public async Task<FileDescriptorDto> GetAsync(Guid id)
    {
        var entity = await _fileRepository.GetAsync(
            id,
            cancellationToken: RequestCancellationToken);
        _containerNameValidator.Validate(entity.ContainerName);
        await AuthorizationService.CheckAsync(entity, CommonOperations.Get);
        return ObjectMapper.Map<FileDescriptor, FileDescriptorDto>(entity);
    }

    /// <summary>
    ///
    /// </summary>
    /// <remarks>
    /// When the current user does not have <see cref="FileExplorerPermissions.Files.Management"/> permission, he can only get his own file
    /// </remarks>
    /// <param name="input"></param>
    /// <returns></returns>
    [Authorize]
    public async Task<PagedResultDto<FileDescriptorDto>> GetListAsync(GetFilesInput input)
    {
        var cancellationToken = RequestCancellationToken;
        _containerNameValidator.Validate(input.ContainerName);
        if (!await AuthorizationService.IsGrantedAsync(FileExplorerPermissions.Files.Management))
        {
            input.CreatorId = CurrentUser.Id;
        }
        var count = await _fileRepository.GetCountAsync(
            input.ContainerName,
            input.CreatorId,
            input.DirectoryId,
            input.Filter,
            input.EntityId,
            cancellationToken);
        var result = await _fileRepository.GetListAsync(
            input.ContainerName,
            input.CreatorId,
            input.DirectoryId,
            input.Filter,
            input.EntityId,
            input.Sorting,
            input.MaxResultCount,
            input.SkipCount,
            cancellationToken);

        return new PagedResultDto<FileDescriptorDto>(
            count,
            ObjectMapper.Map<List<FileDescriptor>, List<FileDescriptorDto>>(result)
            );
    }

    public virtual async Task<IRemoteStreamContent> GetStreamAsync([NotNull] string containerName, [NotNull] string blobName, ImageResizeInput imageResize = null)
    {
        var cancellationToken = RequestCancellationToken;
        _containerNameValidator.Validate(containerName);
        var entity = await _fileManager.GetOrNullAsync(containerName, blobName, cancellationToken);

        if (entity != null)
        {
            await AuthorizationService.CheckAsync(entity, CommonOperations.Get);
            if (!entity.ReferBlobName.IsNullOrEmpty())
            { 
                blobName= entity.ReferBlobName;
            }

            var blobContainer = _blobContainerFactory.Create(containerName);
            Stream stream = await blobContainer.GetOrNullAsync(blobName, cancellationToken);

            if (stream != null)
            {
                if (imageResize != null && (imageResize.Width > 0 || imageResize.Height > 0))
                {
                    var disposeStream = true;
                    try
                    {
                        if ((imageResize.Width ?? 0) > MaxResizeDimension ||
                            (imageResize.Height ?? 0) > MaxResizeDimension)
                        {
                            throw new BusinessException(
                                code: FileStoringImagingErrorCodes.ImageResizeDimensionsTooLarge,
                                message: $"Image resize dimensions cannot exceed {MaxResizeDimension} pixels."
                            );
                        }

                        if (!stream.CanSeek)
                        {
                            throw new BusinessException(
                                code: FileStoringImagingErrorCodes.ImageResizeFailure,
                                message: "Image stream must support seeking."
                            );
                        }

                        var imageInfo = await IdentifyImageAsync(stream);
                        var detectedFormat = imageInfo?.Metadata.DecodedImageFormat;
                        if (detectedFormat != null &&
                            ImageFormatHelper.IsValidImage(detectedFormat.DefaultMimeType, ImageFormatHelper.AllowedImageUploadFormats))
                        {
                            var detectedImageInfo = imageInfo!;
                            var totalPixels = (long)detectedImageInfo.Width * detectedImageInfo.Height;
                            if (detectedImageInfo.Width > MaxResizeDimension ||
                                detectedImageInfo.Height > MaxResizeDimension ||
                                totalPixels > MaxResizePixelCount ||
                                stream.Length > 0 && totalPixels / (double)stream.Length > MaxDecompressionRatio)
                            {
                                throw new BusinessException(
                                    code: FileStoringImagingErrorCodes.ImageTooLarge,
                                    message: "The image is too large to resize safely."
                                );
                            }

                            var cacheKey = $"file-resize:{entity.TenantId}:{containerName}:{blobName}:{entity.Md5}:{imageResize.Width}:{imageResize.Height}";
                            if (_imageResizeCache.TryGetValue<byte[]>(cacheKey, out var cachedImage) && cachedImage is not null)
                            {
                                return new RemoteStreamContent(
                                    new MemoryStream(cachedImage, writable: false),
                                    entity.Name,
                                    detectedFormat.DefaultMimeType,
                                    cachedImage.LongLength,
                                    true);
                            }

                            stream.Position = 0;
                            var result = await _imageResizer.ResizeAsync(
                                stream,
                                new ImageResizeArgs(
                                    imageResize.Width > 0 ? (uint)imageResize.Width : null,
                                    imageResize.Height > 0 ? (uint)imageResize.Height : null,
                                    ImageResizeMode.Crop),
                                detectedFormat.DefaultMimeType
                            );

                            if ((result.State == ImageProcessState.Done ||
                                 result.Result is not null && result.Result.CanRead) && result.Result is not null)
                            {
                                try
                                {
                                    var resizedBytes = await ReadStreamAsync(
                                        result.Result,
                                        MaxResizePixelCount * 4,
                                        cancellationToken);
                                    if (resizedBytes.Length <= MaxCachedImageBytes)
                                    {
                                        _imageResizeCache.Set(
                                            cacheKey,
                                            resizedBytes,
                                            new MemoryCacheEntryOptions
                                            {
                                                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                                                Size = resizedBytes.LongLength
                                            });
                                    }

                                    return new RemoteStreamContent(
                                        new MemoryStream(resizedBytes, writable: false),
                                        entity.Name,
                                        detectedFormat.DefaultMimeType,
                                        resizedBytes.LongLength,
                                        true);
                                }
                                finally
                                {
                                    result.Result.Dispose();
                                }
                            }

                            throw new BusinessException(
                                code: FileStoringImagingErrorCodes.ImageResizeFailure,
                                message: result.State.ToString()
                            );
                        }

                        // A resize request for a non-image falls back to the original
                        // stream. Transfer ownership to the response before leaving the
                        // try block so the finally block does not dispose it early.
                        disposeStream = false;
                        return new RemoteStreamContent(stream, entity.Name, entity.MimeType, stream.Length, true);
                    }
                    finally
                    {
                        if (disposeStream)
                        {
                            stream.Dispose();
                        }
                    }
                }

                return new RemoteStreamContent(stream, entity?.Name, entity?.MimeType, stream.Length, true);
            }
        }
        return null;
    }

    private static async Task<ImageInfo> IdentifyImageAsync(Stream stream)
    {
        using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(DecodeTimeoutSeconds));
        try
        {
            stream.Position = 0;
            var result = await Image.IdentifyAsync(stream, timeout.Token);
            stream.Position = 0;
            return result!;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new BusinessException(
                code: FileStoringImagingErrorCodes.ImageDecodeTimeout,
                message: "Image decoding timed out."
            );
        }
        catch (ImageFormatException exception)
        {
            throw new BusinessException(
                code: FileStoringImagingErrorCodes.ImageFormatNotSupported,
                message: "The image content is invalid.",
                details: exception.Message
            );
        }
    }

    private static async Task<byte[]> ReadStreamAsync(
        Stream source,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > maxBytes)
            {
                throw new BusinessException(
                    code: FileStoringImagingErrorCodes.ImageTooLarge,
                    message: "The resized image is too large."
                );
            }

            await output.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return output.ToArray();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="containerName"></param>
    /// <returns></returns>
    public virtual Task<FileContainerConfigurationDto> GetFileContainerConfigurationAsync([NotNull] string containerName)
    {
        _containerNameValidator.Validate(containerName);
        var dto = new FileContainerConfigurationDto();
        var configuration = _blobContainerConfigurationProvider.Get(containerName);
        var blobSizeLimitConfiguration = configuration.GetFileSizeLimitConfiguration();
        var fileTypeCheckConfiguration = configuration.GetFileTypeCheckConfiguration();
        var authorizationConfiguration = configuration.GetAuthorizationConfiguration();
        dto.MaxBlobSize= blobSizeLimitConfiguration.MaxFileSize;
        dto.AllowedFileTypeNames = fileTypeCheckConfiguration.AllowedFileTypeNames;
        dto.CreateDirectoryPermissionName= authorizationConfiguration.CreateDirectoryPermissionName;
        dto.CreateFilePermissionName= authorizationConfiguration.CreateFilePermissionName;
        dto.UpdateFilePermissionName= authorizationConfiguration.UpdateFilePermissionName;
        dto.DeleteFilePermissionName= authorizationConfiguration.DeleteFilePermissionName;
        dto.GetFilePermissionName= authorizationConfiguration.GetFilePermissionName;
        dto.FileCells = configuration
            .GetFileGridConfiguration()
            .FileCells?
            .Select(c=>
                new FileCellDto(
                    c.Name,
                    c.DisplayName?.Localize(StringLocalizerFactory)
                    )
                ).ToList();

        return Task.FromResult(dto);
    }


    public async Task<ListResultDto<FileDescriptorDto>> GetListByEntityIdAsync([NotNull] string containerName, string entityId)
    {
        var cancellationToken = RequestCancellationToken;
        _containerNameValidator.Validate(containerName);
        //Verify authorization using a virtual file
        var virtualFileDescriptorEntity = new FileDescriptor(GuidGenerator.Create(), containerName, GuidGenerator.Create().ToString(), "virtualFileName.jpg", "image/jpeg", null, null, entityId, CurrentTenant.Id);
        await AuthorizationService.CheckAsync(virtualFileDescriptorEntity, CommonOperations.Get);

        var result = await _fileRepository.GetListAsync(
            containerName,
            null,
            null,
            null,
            entityId,
            cancellationToken: cancellationToken);

        return new ListResultDto<FileDescriptorDto>(
            ObjectMapper.Map<List<FileDescriptor>, List<FileDescriptorDto>>(result)
            );
    }

}
