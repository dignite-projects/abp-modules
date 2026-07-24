using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.FileStoring;

public class ContainerNameValidator : ITransientDependency
{
    private const string DefaultContainerName = "default";
    private readonly IBlobContainerConfigurationProvider _configurationProvider;

    public ContainerNameValidator(IBlobContainerConfigurationProvider configurationProvider = null)
    {
        _configurationProvider = configurationProvider;
    }

    public virtual void Validate(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name), FileConsts.MaxContainerNameLength);

        if (name.Equals(DefaultContainerName, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_configurationProvider == null ||
            ReferenceEquals(
                _configurationProvider.Get(name),
                _configurationProvider.Get(DefaultContainerName)))
        {
            throw new BusinessException(
                code: FileErrorCodes.Containers.NotFound,
                message: $"The BLOB container '{name}' is not registered.");
        }
    }
}
