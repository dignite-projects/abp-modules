using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Abp.BlobManagement.Permissions;

/// <summary>
/// The <c>BlobManagement</c> group: the module-wide permissions, and the resource permissions on a container and on a
/// directory. A consumer may re-point who manages the resource grants in its own provider
/// (<c>context.GetResourcePermissionOrNull(...).ManagementPermissionName = ...</c>).
/// </summary>
public class BlobManagementPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(BlobManagementPermissions.GroupName, L("Permission:BlobManagement"));

        var containers = group.AddPermission(BlobManagementPermissions.Containers.Default, L("Permission:Containers"));
        containers.AddChild(BlobManagementPermissions.Containers.UploadFiles, L("Permission:UploadFiles"));
        containers.AddChild(BlobManagementPermissions.Containers.UpdateFiles, L("Permission:UpdateFiles"));
        containers.AddChild(BlobManagementPermissions.Containers.DeleteFiles, L("Permission:DeleteFiles"));
        containers.AddChild(BlobManagementPermissions.Containers.CreateDirectories, L("Permission:CreateDirectories"));
        containers.AddChild(BlobManagementPermissions.Containers.UpdateDirectories, L("Permission:UpdateDirectories"));
        containers.AddChild(BlobManagementPermissions.Containers.DeleteDirectories, L("Permission:DeleteDirectories"));

        group.AddPermission(BlobManagementPermissions.ManagePermissions, L("Permission:ManagePermissions"));

        AddResourcePermission(context, BlobManagementPermissions.Container.View, BlobManagementResourceNames.Container, "Permission:View");
        AddResourcePermission(context, BlobManagementPermissions.Container.UploadFiles, BlobManagementResourceNames.Container, "Permission:UploadFiles");
        AddResourcePermission(context, BlobManagementPermissions.Container.UpdateFiles, BlobManagementResourceNames.Container, "Permission:UpdateFiles");
        AddResourcePermission(context, BlobManagementPermissions.Container.DeleteFiles, BlobManagementResourceNames.Container, "Permission:DeleteFiles");
        AddResourcePermission(context, BlobManagementPermissions.Container.CreateDirectories, BlobManagementResourceNames.Container, "Permission:CreateDirectories");
        AddResourcePermission(context, BlobManagementPermissions.Container.UpdateDirectories, BlobManagementResourceNames.Container, "Permission:UpdateDirectories");
        AddResourcePermission(context, BlobManagementPermissions.Container.DeleteDirectories, BlobManagementResourceNames.Container, "Permission:DeleteDirectories");

        AddResourcePermission(context, BlobManagementPermissions.Directory.View, BlobManagementResourceNames.Directory, "Permission:View");
        AddResourcePermission(context, BlobManagementPermissions.Directory.CreateDirectories, BlobManagementResourceNames.Directory, "Permission:CreateDirectories");
        AddResourcePermission(context, BlobManagementPermissions.Directory.UpdateDirectories, BlobManagementResourceNames.Directory, "Permission:UpdateDirectories");
        AddResourcePermission(context, BlobManagementPermissions.Directory.DeleteDirectories, BlobManagementResourceNames.Directory, "Permission:DeleteDirectories");
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
