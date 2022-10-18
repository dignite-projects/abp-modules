﻿using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Content;

namespace Dignite.FileExplorer.Files;

[Area("FileExplorer")]
[RemoteService(Name = FileExplorerRemoteServiceConsts.RemoteServiceName)]
[Route("api/file-explorer/files")]
public class FileDescriptorController : AbpController, IFileDescriptorAppService
{
    private readonly IFileDescriptorAppService _fileAppService;

    public FileDescriptorController(
        IFileDescriptorAppService fileAppService
        )
    {
        _fileAppService = fileAppService;
    }

    [HttpGet]
    [Route("{containerName}/configuration")]
    public virtual Task<BlobContainerConfigurationDto> GetBlobContainerConfigurationAsync([NotNull] string containerName)
    {
        return _fileAppService.GetBlobContainerConfigurationAsync(containerName);
    }

    [HttpGet]
    [Route("{containerName}/{*blobName}")]
    public virtual async Task<IRemoteStreamContent> GetFileAsync([NotNull] string containerName, [NotNull] string blobName)
    {
        return await _fileAppService.GetFileAsync(containerName, blobName);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpGet]
    public virtual async Task<PagedResultDto<FileDescriptorDto>> GetListAsync(GetFilesInput input)
    {
        var result = await _fileAppService.GetListAsync(input);

        return result;
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _fileAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("download/{containerName}/{*blobName}")]
    public virtual async Task<FileResult> DownloadAsync([NotNull] string containerName, [NotNull] string blobName, [NotNull] string fileName)
    {
        var file = await _fileAppService.GetFileAsync(containerName, blobName);
        return File(file.GetStream(), file.ContentType, fileName);
    }
}