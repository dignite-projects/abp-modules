﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorise;
using Dignite.FileExplorer.Files;
using Microsoft.AspNetCore.Components;

namespace Dignite.FileExplorer.Blazor.Pages.FileExplorer;
public partial class FileEditComponent
{
    protected readonly IFileDescriptorAppService FileDescriptorAppService;

    /// <summary>
    /// 
    /// </summary>
    protected virtual BlobHandlerConfigurationDto Configuration { get; private set; }

    protected long MaxFileSize = long.MaxValue;

    protected FileEdit FileEditRef;

    protected virtual List<FileUpload> Files { get; set; }

    public FileEditComponent(IFileDescriptorAppService fileDescriptorAppService)
    {
        FileDescriptorAppService = fileDescriptorAppService;
        Configuration = new BlobHandlerConfigurationDto();
    }


    /// <summary>
    /// 
    /// </summary>
    [Parameter]
    public string ContainerName { get; set; }

    /// <summary>
    /// 
    /// </summary>
    [Parameter]
    public string EntityId { get; set; }

    [Parameter]
    public bool Multiple { get; set; }


    /// <summary>
    /// 
    /// </summary>
    [Parameter]
    public List<FileDescriptorDto> SelectedFiles { get; set; }

    [Parameter]
    public EventCallback<List<FileDescriptorDto>> SelectedFilesChanged { get; set; }

    [Parameter]
    public EventCallback<FileChangedEventArgs> Changed { get; set; }


    [Parameter]
    public RenderFragment<List<FileDescriptorDto>> FileDescriptorsContent { get; set; }

    [Parameter]
    public RenderFragment<List<FileUpload>> FilesContent { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Configuration = await FileDescriptorAppService.GetBlobHandlerConfiguration(ContainerName);
        MaxFileSize = Configuration.MaximumBlobSize == 0 ? long.MaxValue : (Configuration.MaximumBlobSize*1024);

        if (!EntityId.IsNullOrEmpty() && SelectedFiles == null)
        {
            SelectedFiles = (await FileDescriptorAppService.GetListAsync(new GetFilesInput
            {
                SkipCount = 0,
                ContainerName = ContainerName,
                EntityId = EntityId,
                MaxResultCount = 1000
            })).Items.ToList();
            await InvokeAsync(() =>
            {
                SelectedFilesChanged.InvokeAsync(SelectedFiles);
            });
        }

        await base.OnInitializedAsync();
    }

    private async Task OnFileChangedAsync(FileChangedEventArgs e)
    {
        if (e.Files.Any())
        {
            Files = Files == null ? new List<FileUpload>() : Files.Where(f => f.Status == FileUploadStatus.Ready).ToList();
            foreach (var file in e.Files)
            {
                var fu = new FileUpload(file);
                if (file.Size > MaxFileSize)
                {
                    fu.Status = FileUploadStatus.Fail;
                    fu.ErrorMessage = L["ExceedsMaximumSize"];
                }

                Files.Add(fu);
            }

            await Changed.InvokeAsync(
                new FileChangedEventArgs(
                    Files
                    .Where(f => f.Status== FileUploadStatus.Ready)
                    .Select(f=>f.File)
                    .ToArray())
                );
        }
    }

    protected internal virtual async Task RemoveFileAsync(FileUpload file)
    {
        Files.Remove(file);

        /* The API to remove a single file is not supported in the current version. It is supported in the advanced version, and this method will be used after the newer version is updated */
        //await FileEditRef.RemoveFile(file.File.Id);

        await Changed.InvokeAsync(
            new FileChangedEventArgs(
                Files
                .Where(f => f.Status == FileUploadStatus.Ready)
                .Select(f => f.File)
                .ToArray())
            );
    }

    protected internal virtual async Task RemoveFileAsync(FileDescriptorDto fileDescriptor)
    {
        if (fileDescriptor.EntityId == EntityId)
        {
            /*
             * Prompt the user whether to delete the original file when removing the file
             * True: delete the original file
               False: Remove file information only
             * */
            if (await Message.Confirm(L["FileWillBeDeletedMessage"]))
            {
                await FileDescriptorAppService.DeleteAsync(fileDescriptor.Id);
            }
        }
        await RemoveItem(fileDescriptor);
    }

    protected virtual async Task RemoveItem(FileDescriptorDto fileDescriptor)
    {
        SelectedFiles.RemoveAll(fd => fd.Id == fileDescriptor.Id);
        await InvokeAsync(() =>
        {
            SelectedFilesChanged.InvokeAsync(SelectedFiles);
        });
    }
}
