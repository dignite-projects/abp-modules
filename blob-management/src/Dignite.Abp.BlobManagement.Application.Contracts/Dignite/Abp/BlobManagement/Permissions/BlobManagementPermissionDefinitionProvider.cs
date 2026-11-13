using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Authorization.Permissions.Resources;
using Volo.Abp.Localization;

namespace Dignite.Abp.BlobManagement.Permissions;

/// <summary>
/// The <c>BlobManagement</c> group: the two module-wide permissions - reading, and managing grants - and the resource
/// permissions on a container, which are the only ones that change directories or files. A consumer may re-point who
/// manages the resource grants in its own provider
/// (<c>context.GetResourcePermissionOrNull(...).ManagementPermissionName = ...</c>).
/// </summary>
public class BlobManagementPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(BlobManagementPermissions.GroupName, L("Permission:BlobManagement"));

        group.AddPermission(BlobManagementPermissions.Containers.Default, L("Permission:Containers"));

        group.AddPermission(BlobManagementPermissions.ManagePermissions, L("Permission:ManagePermissions"));

        AddResourcePermission(context, BlobManagementPermissions.Container.View, BlobManagementResourceNames.Container, "Permission:View");
        AddResourcePermission(context, BlobManagementPermissions.Container.CreateDirectories, BlobManagementResourceNames.Container, "Permission:CreateDirectories");
        AddResourcePermission(context, BlobManagementPermissions.Container.UpdateDirectories, BlobManagementResourceNames.Container, "Permission:UpdateDirectories");
        AddResourcePermission(context, BlobManagementPermissions.Container.DeleteDirectories, BlobManagementResourceNames.Container, "Permission:DeleteDirectories");

        // Only a client's grant counts: granted to a user or a role it would let nothing through, since the file API
        // refuses a token that carries a user.
        AddResourcePermission(context, BlobManagementPermissions.Container.Files, BlobManagementResourceNames.Container, "Permission:Files")
            .WithProviders(ClientResourcePermissionValueProvider.ProviderName);
    }

    private static PermissionDefinition AddResourcePermission(
        IPermissionDefinitionContext context,
        string name,
        string resourceName,
        string displayName)
    {
        return context.AddResourcePermission(name, resourceName, BlobManagementPermissions.ManagePermissions, L(displayName));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<BlobManagementResource>(name);
    }
}
