using Dignite.Abp.FlexFields.Demo.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.FlexFields.Demo.Permissions;

public class DemoPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(DemoPermissions.GroupName, L("Permission:Demo"));

        var products = myGroup.AddPermission(DemoPermissions.Products.Default, L("Permission:Products"));
        products.AddChild(DemoPermissions.Products.Create, L("Permission:Products.Create"));
        products.AddChild(DemoPermissions.Products.Update, L("Permission:Products.Update"));
        products.AddChild(DemoPermissions.Products.Delete, L("Permission:Products.Delete"));

        var productFields = myGroup.AddPermission(DemoPermissions.ProductFields.Default, L("Permission:ProductFields"));
        productFields.AddChild(DemoPermissions.ProductFields.Create, L("Permission:ProductFields.Create"));
        productFields.AddChild(DemoPermissions.ProductFields.Update, L("Permission:ProductFields.Update"));
        productFields.AddChild(DemoPermissions.ProductFields.Delete, L("Permission:ProductFields.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DemoResource>(name);
    }
}
