using System;
using System.Threading.Tasks;
using Dignite.Drive.Files;
using Volo.Abp.DependencyInjection;

namespace Dignite.Drive;

/// <summary>
/// The open file service with <see cref="DriveFileAppService.PrepareNewFileAsync"/> overridden, as an implementation that
/// keeps fields of its own on the file would. Exposed as itself only, so <see cref="IDriveFileAppService"/> stays the
/// open one in every other test.
/// </summary>
[ExposeServices(typeof(ConfiguringDriveFileAppService))]
public class ConfiguringDriveFileAppService : DriveFileAppService
{
    public ConfiguringDriveFileAppService(
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

    protected override Task<Action<FileDescriptor>?> PrepareNewFileAsync(CreateDriveFileInput input)
    {
        Prepared++;
        return Task.FromResult(ConfigureFile);
    }
}
