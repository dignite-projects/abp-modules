namespace Dignite.Abp.FlexFields.Demo.Permissions;

public static class DemoPermissions
{
    public const string GroupName = "Demo";

    public static class Products
    {
        public const string Default = GroupName + ".Products";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class ProductFields
    {
        public const string Default = GroupName + ".ProductFields";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }
}
