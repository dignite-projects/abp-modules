using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Files;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The open file service with <see cref="BlobManagementFileAppService.PrepareNewFileAsync"/> overridden, as an implementation that
/// keeps fields of its own on the file would. Exposed as itself only, so <see cref="IBlobManagementFileAppService"/> stays the
/// open one in every other test.
/// </summary>
[ExposeServices(typeof(ConfiguringBlobManagementFileAppService))]
public class ConfiguringBlobManagementFileAppService : BlobManagementFileAppService
{
    public ConfiguringBlobManagementFileAppService(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager)
        : base(fileRepository, fileDescriptorManager)
    {
    }

    [DisablePropertyInjection]
    public Action<FileDescriptor>? ConfigureFile { get; set; }

    /// <summary>How many times the hook was asked.</summary>
    [DisablePropertyInjection]
    public int Prepared { get; private set; }

    protected override Task<Action<FileDescriptor>?> PrepareNewFileAsync(CreateBlobManagementFileInput input)
    {
        Prepared++;
        return Task.FromResult(ConfigureFile);
    }
}
