using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

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

        // A client's grant, which is the deployment's: on the host side only, so a tenant's administrator never sees it.
        // Only a client's grant counts, and BlobManagementClientChecker sees to it - a token that carries a user is
        // refused before any grant is read - so a grant to a user or a role lets nothing through. It is not restricted
        // to the client provider: ABP's dialog saves every permission of a resource for whoever it edits, and would
        // fail on one that refuses users and roles.
        AddResourcePermission(context, BlobManagementPermissions.Container.Files, BlobManagementResourceNames.Container, "Permission:Files",
            MultiTenancySides.Host);
    }

    private static PermissionDefinition AddResourcePermission(
        IPermissionDefinitionContext context,
        string name,
        string resourceName,
        string displayName,
        MultiTenancySides multiTenancySide = MultiTenancySides.Both)
    {
        return context.AddResourcePermission(
            name, resourceName, BlobManagementPermissions.ManagePermissions, L(displayName), multiTenancySide);
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<BlobManagementResource>(name);
    }
}
