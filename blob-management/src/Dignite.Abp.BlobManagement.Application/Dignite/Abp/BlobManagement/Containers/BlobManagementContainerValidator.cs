using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>
/// Checks the managed containers once the host has configured them, when the application starts, and fails the start
/// on a configuration that cannot work: the default container declared a managed container, which would make every
/// container name one.
/// </summary>
public class BlobManagementContainerValidator : ITransientDependency
{
    protected AbpBlobStoringOptions BlobStoringOptions { get; }

    public BlobManagementContainerValidator(IOptions<AbpBlobStoringOptions> blobStoringOptions)
    {
        BlobStoringOptions = blobStoringOptions.Value;
    }

    /// <summary>Throws <see cref="AbpException"/> for a configuration that cannot work.</summary>
    public virtual void Validate()
    {
        if (BlobStoringOptions.Containers.GetConfiguration<DefaultContainer>().GetBlobManagementContainerConfiguration().IsManaged)
        {
            throw new AbpException(
                "The default BLOB container is declared a managed container, which makes every container name one. Call UseBlobManagementContainer on named containers only.");
        }
    }
}
