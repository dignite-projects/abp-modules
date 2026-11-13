using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Abp.BlobManagement.Permissions;

/// <summary>
/// The <c>BlobManagement</c> group: the two module-wide permissions - reading, and managing grants - and the resource
/// permissions on a container, which are the only ones that change directories. A consumer may re-point who manages
/// the resource grants in its own provider
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
    }

    private static void AddResourcePermission(
        IPermissionDefinitionContext context,
        string name,
        string resourceName,
        string displayName)
    {
        context.AddResourcePermission(name, resourceName, BlobManagementPermissions.ManagePermissions, L(displayName));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<BlobManagementResource>(name);
    }
}
