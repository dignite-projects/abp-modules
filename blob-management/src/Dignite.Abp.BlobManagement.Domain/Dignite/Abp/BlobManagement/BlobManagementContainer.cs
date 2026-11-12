using Volo.Abp;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// A Blob Management container as a permission resource (<see cref="BlobManagementResourceNames.Container"/>), keyed by its name. A
/// container is configuration, not an entity, so this value object stands for it where a resource grant needs an
/// object: <c>IAuthorizationService.IsGrantedAsync(new BlobManagementContainer("documents"), BlobManagementPermissions.Container.Upload)</c>.
/// Its full name is the resource name of the grants on it: never rename the class.
/// </summary>
public class BlobManagementContainer : IKeyedObject
{
    public BlobManagementContainer(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), FileDescriptorConsts.MaxContainerNameLength);
    }

    public string Name { get; }

    public string? GetObjectKey()
    {
        return Name;
    }
}
