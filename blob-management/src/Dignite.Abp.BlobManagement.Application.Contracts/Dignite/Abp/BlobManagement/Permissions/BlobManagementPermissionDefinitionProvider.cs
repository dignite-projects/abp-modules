using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Abp.BlobManagement.Permissions;

/// <summary>The <c>Blob Management</c> group and its module-wide file permissions.</summary>
public class BlobManagementPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(BlobManagementPermissions.GroupName, L("Permission:BlobManagement"));

        var files = group.AddPermission(BlobManagementPermissions.Files.Default, L("Permission:Files"));
        files.AddChild(BlobManagementPermissions.Files.Upload, L("Permission:Files.Upload"));
        files.AddChild(BlobManagementPermissions.Files.Delete, L("Permission:Files.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<BlobManagementResource>(name);
    }
}
